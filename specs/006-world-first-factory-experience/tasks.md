---
description: "Dependency-ordered implementation work for the World-First Factory Experience"
---

# Tasks: World-First Factory Experience

**Input**: Approved artifacts in `specs/006-world-first-factory-experience/`

**Prerequisites**:
- `spec.md`, clarified 2026-11-11, including the plan-review answers.
- `plan.md`, revision 2: owner decisions OD-1 to OD-16; OD-9 to OD-12 are still pending.
- `research.md`, R01–R23.
- `data-model.md`.
- `contracts/world-presentation.md`, `contracts/presentation-model.md` and `contracts/ui-surfaces-f006.md`.
- `quickstart.md`.
- `reviews/discovery.md` and `reviews/plan-reviews.md`.
- The constitution.

**Tests**: Every implementation Issue ends with its named tests green. Run `scripts/test-unity.ps1 -TestFile <file>` first, then the suite of its ledger unit. Art tooling runs through `scripts/test-art-pipeline.ps1`. Test work belongs to each task and is never a separate red-only Issue. Every Unity run recompiles first, fails when the total test count is 0, and records the count (R19). Human and agent evaluation gates are *Parent* tasks: the owner decisions and the SC-004, SC-005, SC-006 and SC-012 trials.

**Implementation lane**: MAQA is proposed, as in F004. The final lane choice is made by the parent at T500.
- Each task is one MAQA Issue unless it is marked *Parent*.
- Issues are created only by an owner-approved `$speckit-maqa-sync` run.
- Every accepted dependency is merged to `origin/main` before its dependents start.
- After QA PASS the parent merges only through `scripts/maqa-merge.ps1 -Issue <n>`.

**Roles and skills**:
- Role sections come from `docs/agents/roles-and-reviews.md` (it includes the World Designer, `01c9001`). Workstation rules come from `docs/agents/workstation-pitfalls.md`.
- Claude Code workers may use these `unity:*` skills as knowledge only:
  - `unity:ui-uitk` for UI tasks;
  - `unity:2d-pixel-perfect` for camera tasks;
  - `unity:create-tile-palette-or-rule-tiles` for the deck;
  - `unity:manage-sprite-atlas` for import.

  Each worker reads `docs/agents/unity-plugin-skills.md` first.
- Model tiers follow `AGENTS.md`: Medium (`sonnet`) by default. T510, T511, T512 and T514 run at Medium with `high` reasoning.

**Shared resources**:
- `unity`, `emulator` and `gpu` are leased through `scripts/resource-lease.ps1` with owner `issue-<n>`.
- Any run that may have Android active, and every `-StandaloneWindowsPlayer` run, needs `unity,emulator`.
- Blender renders need `gpu`.
- B4 needs `unity,emulator,gpu`.

**Task IDs**: T500–T551. GitHub has no `maqa` task ID above T476 (checked 2026-11-11), and IDs are unique across the repository.

**Scene and settings rule**: Scenes, prefabs, `.asset` files and `ProjectSettings/TagManager.asset` change only through Unity tooling: MCP, the Inspector, or a temporary `-executeMethod` editor script. They are never edited as hand-written YAML. A deliberate settings change is committed with `IDLEFACTORY_ALLOW_SETTINGS_NOISE=1` and a recorded decision.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: may run concurrently after its dependencies, on disjoint paths and worktrees. A Unity lease still serializes Unity runs.
- **[US#]**: user story in `spec.md`. Setup, foundational and closeout tasks carry no story label.
- Every task names its role and lenses, paths, validation, resources and the requirements it covers.
- *Parent* tasks have no MAQA Issue. The parent ticks them manually after verification.

## Issue mapping

| Task | Issue | Resources | Notes |
|---|---|---|---|
| T500, T501, T504, T509, T518, T530, T546, T549–T551 | *parent* | varies | Gates, owner decisions, trials, closeout |
| T502 | new | unity, emulator | Baseline probes and matrix |
| T503, T547 | new | none | World Designer documents and conformance |
| T505 | new | unity | Compositor (offline) and geometry exporter (Edit Mode) |
| T506, T519–T521 | new | gpu | Concept and Blender renders |
| T507, T522, T523 | new | none | Scripted art, mockup sheets |
| T524 | new | unity | Scripted badge re-anchor plus `StateCueSetTests` re-baseline |
| T508, T525, T548 | new | none | Independent read-only reviews |
| T510–T515, T526–T529, T532–T540 | new | unity (emulator when Android is active) | Unity implementation |
| T516, T517 | new | none | Art Bible draft, anchor measurement |
| T531, T544 | new | unity, emulator, gpu | B4a and B4b |
| T541 | new | none | Emulator script changes (validated without an emulator) |
| T542, T543, T545 | new | unity, emulator | Full verification, Play-Tester, trial captures |

---

## Context Orchestration Process

### Parent Orchestrator Rules

- **Order.** Execute work units in ledger order. Units the ledger marks as parallel candidates may run together once their prerequisites are verified. Task-level `[P]` markers are local hints only.
- **Completion.** Task checkboxes are the implementation completion checklist. A unit is complete only when every referenced task is checked after parent verification. GitHub Issues remain the MAQA workflow-status authority, and checkboxes are mirrored from them (`scripts/maqa-sync-task-checkboxes.ps1`).
- **Fresh sessions.** Use a clean subagent session for each delegated unit, with one authoritative MAQA Issue and one worktree. Include the full handoff packet; do not rely on prior conversation state.
- **Write scope.** Keep each worker inside its listed write scope. A worker stops before editing an unassigned path or a shared serialized Unity asset. Scenes, prefabs, `.asset` files and `ProjectSettings/TagManager.asset` change only through Unity tooling.
- **Tests.** Test tasks require fail-first behavior where practical. A red-only unit is never accepted.
- **Leases.** Acquire the leases a unit needs before spawning its worker, and release them after QA and merge. Use `scripts/resource-lease.ps1` with owner `issue-<n>`:
  - `unity` for every Unity run;
  - `emulator` for player, Android or performance runs;
  - `gpu` for Blender and performance runs.
- **Owner gates are blocking:**
  - OD-9a (T504) before any direction brief;
  - OD-9b (T509) before P2/P3 production;
  - OD-10 and OD-12 (T518) before family production;
  - OD-11 (T500) before T514.

  P3 batches A, B and C each end with T543, and the next batch starts only after it.
- **Model tier.** Choose the tier per the AGENTS.md routing: Medium by default, `high` reasoning for T510, T511, T512 and T514, Low for checklist reviews. Escalate only with a recorded reason.
- **Independent review.** Producers never review their own assets, mockups, Art Bible text or World Bible text. Reviewers are fresh sessions (research R18).
- **Parent checks.** The parent inspects diffs, runs targeted verification, checks for unrelated changes, and fixes or re-delegates inconsistencies before starting a dependent unit.
- **Final validation.** The parent owns final validation and reruns the completion gates even when another agent helped interpret results. Workers and QA never run the `after_implement` hooks.

### Required Subagent Handoff Packet

Every assignment includes:

- the work unit ID, phase, task ID, MAQA Issue, exact task text and exact paths;
- the role, review lenses, complexity tier and model/reasoning level;
- the relevant `spec.md` FR, SC and edge cases, `plan.md` Design sections and owner decisions OD-1 to OD-16, `research.md` R-ids, `data-model.md` rows and `contracts/` sections;
- the non-negotiable constraints:
  - no production, economy, content, unlock, offline, prestige or save change (FR-001);
  - presentation derives from authoritative state, with no per-item simulation (FR-002);
  - presentation state is never saved (FR-003);
  - integer scales, D24, the 70×54 lattice and the palette cap of 48 (FR-004);
  - one camera writer, with selection-driven docked framing;
  - no per-machine UI Toolkit elements;
  - no fixed slot count in new code;
  - zero steady-state allocations;
  - for visual work, the World Bible and Art Bible versions and the approved brief;
- the accepted prerequisite commits and the current implementation status;
- the allowed and forbidden paths and tools. Blender MCP is allowed only for production units that name it, after a real tool call in the worker session;
- Unity `6000.6.0f1`, the target platforms, and the required Edit Mode, Play Mode, player or pipeline validation with the exact commands;
- the relevant wiki pages named in the unit's handoff sources;
- the required response:
  - files changed;
  - task IDs completed;
  - tests run, with results and counts;
  - expected failures;
  - assumptions;
  - unresolved risks;
  - parent follow-up;
  - a done/blocked verdict.

### Parent Verification Gate

For each unit, the parent:

1. Inspects `git diff -- <unit paths>` plus every extra path the worker reported, and confirms edits stay in scope or are justified adjacent changes.
2. Runs the targeted `scripts/test-unity.ps1 -TestFile <file>` commands, `scripts/test-art-pipeline.ps1` where art tooling or assets changed, and the suite named in the unit.
3. Rereads the contract and data-model sections for any public boundary change: the layout seam, `CompletedCycles`, the navigation stack, or preferences v2.
4. Confirms the following are unchanged unless the unit names them:
   - Core (except in T514);
   - content and save paths;
   - `Library/`;
   - unassigned serialized assets;
   - `ProjectSettings/` noise, checked with `scripts/check-settings-noise.ps1 -Commit <sha>` (TagManager changes only in T513).
5. Requires independent QA PASS on the exact commit, plus the named specialist reviews, before marking the unit.

### Context Compaction Recovery

After compaction:

1. Read this process, the ledger and the checklist.
2. Run `git status --short` and `scripts/resource-lease.ps1 -Status`, and inspect existing diffs.
3. Resume at an earlier partially changed unit if one exists. Otherwise resume at the first unit with unchecked tasks whose prerequisites are accepted.
4. Reconstruct state from Issue status, merged commits, mirrored checkboxes, diffs, review records and targeted tests.
5. Finish and verify that unit before opening another worker.

### Work Unit Ledger

Ledger convention:

- One unit per task.
- Rows are in dependency (execution) order.
- Every prerequisite names an earlier unit.
- The Tasks column is the only mapping between units and tasks.

Generated 2026-11-11 after the `$speckit-analyze` remediation (52 units).

| Unit | Phase | Tasks | Atomic scope and touched paths | Prerequisites | Required handoff sources | Parent verification |
|------|-------|-------|--------------------------------|---------------|--------------------------|---------------------|
| WU01 | Phase 1: Setup and gates | T500 | Parent-only. `specs/006-world-first-factory-experience/tasks.md`, `plan.md` | None | spec.md, plan.md, reviews/plan-reviews.md | Parent records the decision or gate result with date and evidence links; diff limited to the named records. |
| WU02 | Phase 1: Setup and gates | T501 | Parent-only. `reviews/discovery.md`, `reviews/plan-reviews.md` | None | spec.md, plan.md, reviews/plan-reviews.md | Parent records the decision or gate result with date and evidence links; diff limited to the named records. |
| WU03 | Phase 2: User Story 8 (direction gate) — Baseline, World Bible and mockups (P1, phase P0) | T502 | `Assets/IdleFactory/Tests/PlayMode/F006/ShellGeometryProbe.cs`, `ShellGeometryProbeTests.cs`, `InteractionCountJourney.cs`, `reviews/baseline.md` | WU01 | spec.md US8, FR-040–FR-047; research.md R02, R03, R07, R22; contracts/world-presentation.md §6–7; wiki/pages/f006-plan-overview.md | `scripts/test-unity.ps1 -TestFile` for ShellGeometryProbeTests (recompile first, total > 0); `scripts/check-settings-noise.ps1 -Commit <sha>`; diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU04 | Phase 2: User Story 8 (direction gate) — Baseline, World Bible and mockups (P1, phase P0) | T503 | `docs/world/world-bible-v0.1.md`, `specs/006-world-first-factory-experience/reviews/world-copy.md` | WU01 | spec.md US8, FR-040–FR-047; research.md R02, R03, R07, R22; contracts/world-presentation.md §6–7; wiki/pages/f006-plan-overview.md | diff inspection against the listed paths plus the closest validation named in the task; diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU05 | Phase 2: User Story 8 (direction gate) — Baseline, World Bible and mockups (P1, phase P0) | T504 | Parent-only. `docs/world/world-bible-v0.1.md`, `specs/006-world-first-factory-experience/reviews/world-copy.md` | WU04 | spec.md US8, FR-040–FR-047; research.md R02, R03, R07, R22; contracts/world-presentation.md §6–7; wiki/pages/f006-plan-overview.md | Parent records the decision or gate result with date and evidence links; diff limited to the named records. |
| WU06 | Phase 2: User Story 8 (direction gate) — Baseline, World Bible and mockups (P1, phase P0) | T505 | `Assets/IdleFactory/Tests/EditMode/F006/LatticeGeometryExportTests.cs`, `tools/art-pipeline/f006/lattice-geometry.json`, `tools/art-pipeline` | WU01 | spec.md US8, FR-040–FR-047; research.md R02, R03, R07, R22; contracts/world-presentation.md §6–7; wiki/pages/f006-plan-overview.md | `powershell -NoProfile -File scripts/test-art-pipeline.ps1` exit 0 (3 = blocker); `scripts/test-unity.ps1 -TestFile` for LatticeGeometryExportTests (recompile first, total > 0); `scripts/check-settings-noise.ps1 -Commit <sha>`; diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU07 | Phase 2: User Story 8 (direction gate) — Baseline, World Bible and mockups (P1, phase P0) | T506 | `art-source/f006/concept/` | WU05 | spec.md US8, FR-040–FR-047; research.md R02, R03, R07, R22; contracts/world-presentation.md §6–7; wiki/pages/f006-plan-overview.md | `powershell -NoProfile -File scripts/test-art-pipeline.ps1` exit 0 (3 = blocker); diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU08 | Phase 2: User Story 8 (direction gate) — Baseline, World Bible and mockups (P1, phase P0) | T507 | `specs/006-world-first-factory-experience/reviews/direction/` | WU06, WU07 | spec.md US8, FR-040–FR-047; research.md R02, R03, R07, R22; contracts/world-presentation.md §6–7; wiki/pages/f006-plan-overview.md | diff inspection against the listed paths plus the closest validation named in the task; diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU09 | Phase 2: User Story 8 (direction gate) — Baseline, World Bible and mockups (P1, phase P0) | T508 | Read-only review. `Core-Idea.md`, `specs/006-world-first-factory-experience/reviews/direction.md` | WU08 | spec.md US8, FR-040–FR-047; research.md R02, R03, R07, R22; contracts/world-presentation.md §6–7; wiki/pages/f006-plan-overview.md | Review record exists at the named path with verdicts and findings; the reviewer is a fresh session and not the producer; no code diff. |
| WU10 | Phase 2: User Story 8 (direction gate) — Baseline, World Bible and mockups (P1, phase P0) | T509 | Parent-only. `reviews/direction.md`, `world-copy.md` | WU09 | spec.md US8, FR-040–FR-047; research.md R02, R03, R07, R22; contracts/world-presentation.md §6–7; wiki/pages/f006-plan-overview.md | Parent records the decision or gate result with date and evidence links; diff limited to the named records. |
| WU11 | Phase 3: Foundational refactor and User Story 7 — Not bound to 12 (P1 enabling, US7 P2) | T510 | `Assets/IdleFactory/Unity/Presentation/World/SlotGridView.cs`, `SlotVisualModel.cs`, `SlotLattice.cs`, `FloorRange.cs`, `RunZoomSettings.cs`, `SlotRenderers.cs`, `scripts/test-unity.ps1`, `.asset` | WU01 | spec.md US7, FR-037, FR-038; research.md R04, R05, R06, R08, R11, R19, R22; data-model.md; contracts/world-presentation.md §4, contracts/presentation-model.md §1; wiki/pages/f006-world-presentation-design.md, pixel-camera-scale.md, run-shell-and-panels.md | `scripts/check-settings-noise.ps1 -Commit <sha>`; diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU12 | Phase 3: Foundational refactor and User Story 7 — Not bound to 12 (P1 enabling, US7 P2) | T511 | `Assets/IdleFactory/Unity/UI/RunShellDocument.cs`, `Assets/IdleFactory/Unity/UI/ShellNavigationStack.cs`, `ShellSheetHost.cs`, `Assets/IdleFactory/Tests/EditMode/F006/ShellNavigationStackTests.cs` | WU01 | spec.md US7, FR-037, FR-038; research.md R04, R05, R06, R08, R11, R19, R22; data-model.md; contracts/world-presentation.md §4, contracts/presentation-model.md §1; wiki/pages/f006-world-presentation-design.md, pixel-camera-scale.md, run-shell-and-panels.md | `scripts/test-unity.ps1 -TestFile` for ShellNavigationStackTests, RunKeyboardFocusTests, RunHudLayoutTests, AndroidBackGestureTests (recompile first, total > 0); `scripts/check-settings-noise.ps1 -Commit <sha>`; diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU13 | Phase 3: Foundational refactor and User Story 7 — Not bound to 12 (P1 enabling, US7 P2) | T512 | `Assets/IdleFactory/Unity/Presentation/World/`, `FactoryWorldLayout.cs`, `LatticeLayoutBuilder.cs`, `BuildingLocationId.cs`, `data-model.md`, `contracts/world-presentation.md`, `WorldCompositionSettings.cs`, `Assets/IdleFactory/Art/F006/WorldCompositionSettings.asset`, `Assets/IdleFactory/Tests/*/F006/`, `tools/art-pipeline/f006/lattice-geometry.json` | WU06, WU11 | spec.md US7, FR-037, FR-038; research.md R04, R05, R06, R08, R11, R19, R22; data-model.md; contracts/world-presentation.md §4, contracts/presentation-model.md §1; wiki/pages/f006-world-presentation-design.md, pixel-camera-scale.md, run-shell-and-panels.md | `powershell -NoProfile -File scripts/test-art-pipeline.ps1` exit 0 (3 = blocker); `scripts/test-unity.ps1 -TestFile` for WorldLayoutParityTests, WorldLayoutValidationTests, LayoutIndependenceTests, NoFixedSlotCountGuardTests, SlotGridLatticeTests, SlotGridTests (recompile first, total > 0); `scripts/check-settings-noise.ps1 -Commit <sha>`; diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU14 | Phase 3: Foundational refactor and User Story 7 — Not bound to 12 (P1 enabling, US7 P2) | T513 | `ProjectSettings/TagManager.asset`, `Assets/IdleFactory/Tests/EditMode/F006/SortingLayerGuardTests.cs`, `reviews/test-rebaseline.md` | WU01 | spec.md US7, FR-037, FR-038; research.md R04, R05, R06, R08, R11, R19, R22; data-model.md; contracts/world-presentation.md §4, contracts/presentation-model.md §1; wiki/pages/f006-world-presentation-design.md, pixel-camera-scale.md, run-shell-and-panels.md | `scripts/test-unity.ps1 -TestFile` for SortingLayerGuardTests (recompile first, total > 0); `scripts/check-settings-noise.ps1 -Commit <sha>`; diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU15 | Phase 3: Foundational refactor and User Story 7 — Not bound to 12 (P1 enabling, US7 P2) | T514 | `contracts/presentation-model.md`, `Assets/IdleFactory/Core/Run/Simulation/RunSimulator.cs`, `Assets/IdleFactory/Core/Run/Application/RunSession.cs`, `MachineStateView.cs`, `Assets/IdleFactory/Tests/EditMode/F006/`, `reviews/test-rebaseline.md` | WU01 | spec.md US7, FR-037, FR-038; research.md R04, R05, R06, R08, R11, R19, R22; data-model.md; contracts/world-presentation.md §4, contracts/presentation-model.md §1; wiki/pages/f006-world-presentation-design.md, pixel-camera-scale.md, run-shell-and-panels.md | `scripts/test-unity.ps1 -TestFile` for CompletedCyclesLifecycleTests, CompletedCyclesPropertyTests, CompletedCyclesNoObserverEffectTests (recompile first, total > 0); `scripts/check-settings-noise.ps1 -Commit <sha>`; diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU16 | Phase 3: Foundational refactor and User Story 7 — Not bound to 12 (P1 enabling, US7 P2) | T515 | `specs/006-world-first-factory-experience/reviews/test-rebaseline.md` | WU12 | spec.md US7, FR-037, FR-038; research.md R04, R05, R06, R08, R11, R19, R22; data-model.md; contracts/world-presentation.md §4, contracts/presentation-model.md §1; wiki/pages/f006-world-presentation-design.md, pixel-camera-scale.md, run-shell-and-panels.md | diff inspection against the listed paths plus the closest validation named in the task; diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU17 | Phase 4: User Story 2 — A convincing industrial place (P1, phase P2, art first) | T516 | `docs/art/F004-art-bible-v1.md`, `contracts/world-presentation.md` | WU10 | spec.md US2, FR-004, FR-010a–FR-015, FR-044; research.md R06, R07, R12, R16, R17, R23; contracts/world-presentation.md §1–5, contracts/presentation-model.md §4; docs/art/F004-art-bible-v1.md; docs/world/world-bible-v0.1.md; wiki/pages/art-production-pipeline.md, f006-world-presentation-design.md | diff inspection against the listed paths plus the closest validation named in the task; diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU18 | Phase 4: User Story 2 — A convincing industrial place (P1, phase P2, art first) | T517 | `specs/006-world-first-factory-experience/reviews/a03-anchor/`, `reviews/a03-anchor.md`, `tools/art-pipeline` | WU10 | spec.md US2, FR-004, FR-010a–FR-015, FR-044; research.md R06, R07, R12, R16, R17, R23; contracts/world-presentation.md §1–5, contracts/presentation-model.md §4; docs/art/F004-art-bible-v1.md; docs/world/world-bible-v0.1.md; wiki/pages/art-production-pipeline.md, f006-world-presentation-design.md | `powershell -NoProfile -File scripts/test-art-pipeline.ps1` exit 0 (3 = blocker); diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU19 | Phase 4: User Story 2 — A convincing industrial place (P1, phase P2, art first) | T518 | Parent-only. `plan.md` | WU17, WU18 | spec.md US2, FR-004, FR-010a–FR-015, FR-044; research.md R06, R07, R12, R16, R17, R23; contracts/world-presentation.md §1–5, contracts/presentation-model.md §4; docs/art/F004-art-bible-v1.md; docs/world/world-bible-v0.1.md; wiki/pages/art-production-pipeline.md, f006-world-presentation-design.md | Parent records the decision or gate result with date and evidence links; diff limited to the named records. |
| WU20 | Phase 4: User Story 2 — A convincing industrial place (P1, phase P2, art first) | T519 | `art-source/f006/ground/`, `art-source/f006/foundation/`, `scripts/test-art-pipeline.ps1` | WU19 | spec.md US2, FR-004, FR-010a–FR-015, FR-044; research.md R06, R07, R12, R16, R17, R23; contracts/world-presentation.md §1–5, contracts/presentation-model.md §4; docs/art/F004-art-bible-v1.md; docs/world/world-bible-v0.1.md; wiki/pages/art-production-pipeline.md, f006-world-presentation-design.md | `powershell -NoProfile -File scripts/test-art-pipeline.ps1` exit 0 (3 = blocker); diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU21 | Phase 4: User Story 2 — A convincing industrial place (P1, phase P2, art first) | T520 | `art-source/f006/depot/`, `art-source/f006/deposit/` | WU19 | spec.md US2, FR-004, FR-010a–FR-015, FR-044; research.md R06, R07, R12, R16, R17, R23; contracts/world-presentation.md §1–5, contracts/presentation-model.md §4; docs/art/F004-art-bible-v1.md; docs/world/world-bible-v0.1.md; wiki/pages/art-production-pipeline.md, f006-world-presentation-design.md | `powershell -NoProfile -File scripts/test-art-pipeline.ps1` exit 0 (3 = blocker); diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU22 | Phase 4: User Story 2 — A convincing industrial place (P1, phase P2, art first) | T521 | `art-source/f006/prop/`, `art-source/f006/edge/` | WU19 | spec.md US2, FR-004, FR-010a–FR-015, FR-044; research.md R06, R07, R12, R16, R17, R23; contracts/world-presentation.md §1–5, contracts/presentation-model.md §4; docs/art/F004-art-bible-v1.md; docs/world/world-bible-v0.1.md; wiki/pages/art-production-pipeline.md, f006-world-presentation-design.md | `powershell -NoProfile -File scripts/test-art-pipeline.ps1` exit 0 (3 = blocker); diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU23 | Phase 4: User Story 2 — A convincing industrial place (P1, phase P2, art first) | T522 | `art-source/f006/link/`, `art-source/f006/fx/` | WU19 | spec.md US2, FR-004, FR-010a–FR-015, FR-044; research.md R06, R07, R12, R16, R17, R23; contracts/world-presentation.md §1–5, contracts/presentation-model.md §4; docs/art/F004-art-bible-v1.md; docs/world/world-bible-v0.1.md; wiki/pages/art-production-pipeline.md, f006-world-presentation-design.md | `powershell -NoProfile -File scripts/test-art-pipeline.ps1` exit 0 (3 = blocker); diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU24 | Phase 4: User Story 2 — A convincing industrial place (P1, phase P2, art first) | T523 | `art-source/f006/ui/`, `scripts/test-art-pipeline.ps1` | WU19 | spec.md US2, FR-004, FR-010a–FR-015, FR-044; research.md R06, R07, R12, R16, R17, R23; contracts/world-presentation.md §1–5, contracts/presentation-model.md §4; docs/art/F004-art-bible-v1.md; docs/world/world-bible-v0.1.md; wiki/pages/art-production-pipeline.md, f006-world-presentation-design.md | `powershell -NoProfile -File scripts/test-art-pipeline.ps1` exit 0 (3 = blocker); diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU25 | Phase 4: User Story 2 — A convincing industrial place (P1, phase P2, art first) | T524 | `Assets/IdleFactory/Unity/Presentation/StateCueSet.cs`, `art-source/f006/`, `Assets/IdleFactory/Tests/EditMode/StateCueSetTests.cs` | WU19 | spec.md US2, FR-004, FR-010a–FR-015, FR-044; research.md R06, R07, R12, R16, R17, R23; contracts/world-presentation.md §1–5, contracts/presentation-model.md §4; docs/art/F004-art-bible-v1.md; docs/world/world-bible-v0.1.md; wiki/pages/art-production-pipeline.md, f006-world-presentation-design.md | `powershell -NoProfile -File scripts/test-art-pipeline.ps1` exit 0 (3 = blocker); `scripts/test-unity.ps1 -TestFile` for StateCueSetTests (recompile first, total > 0); `scripts/check-settings-noise.ps1 -Commit <sha>`; diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU26 | Phase 4: User Story 2 — A convincing industrial place (P1, phase P2, art first) | T525 | Read-only review. `specs/006-world-first-factory-experience/reviews/art-direction-f006.md`, `reviews/world-bible-conformance.md` | WU20, WU21, WU22, WU23, WU24, WU25 | spec.md US2, FR-004, FR-010a–FR-015, FR-044; research.md R06, R07, R12, R16, R17, R23; contracts/world-presentation.md §1–5, contracts/presentation-model.md §4; docs/art/F004-art-bible-v1.md; docs/world/world-bible-v0.1.md; wiki/pages/art-production-pipeline.md, f006-world-presentation-design.md | Review record exists at the named path with verdicts and findings; the reviewer is a fresh session and not the producer; no code diff. |
| WU27 | Phase 4: User Story 2 — A convincing industrial place (P1, phase P2, art first) | T526 | `Assets/IdleFactory/Art/F006/`, `Assets/IdleFactory/Tests/EditMode/F006/F006AssetContractTests.cs`, `scripts/test-art-pipeline.ps1`, `scripts/test-unity.ps1` | WU14, WU26 | spec.md US2, FR-004, FR-010a–FR-015, FR-044; research.md R06, R07, R12, R16, R17, R23; contracts/world-presentation.md §1–5, contracts/presentation-model.md §4; docs/art/F004-art-bible-v1.md; docs/world/world-bible-v0.1.md; wiki/pages/art-production-pipeline.md, f006-world-presentation-design.md | `powershell -NoProfile -File scripts/test-art-pipeline.ps1` exit 0 (3 = blocker); `scripts/test-unity.ps1 -TestFile` for F006AssetContractTests (recompile first, total > 0); `scripts/check-settings-noise.ps1 -Commit <sha>`; diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU28 | Phase 4: User Story 2 — A convincing industrial place (P1, phase P2, art first) | T527 | `Assets/IdleFactory/Unity/Presentation/World/FactoryDeckView.cs` | WU13, WU27 | spec.md US2, FR-004, FR-010a–FR-015, FR-044; research.md R06, R07, R12, R16, R17, R23; contracts/world-presentation.md §1–5, contracts/presentation-model.md §4; docs/art/F004-art-bible-v1.md; docs/world/world-bible-v0.1.md; wiki/pages/art-production-pipeline.md, f006-world-presentation-design.md | `scripts/test-unity.ps1 -TestFile` for DeckLayoutTests, DeckRenderTests, ReducedMotionWorldTests (recompile first, total > 0); `scripts/check-settings-noise.ps1 -Commit <sha>`; diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU29 | Phase 5: User Story 4 — Read the factory's health at a glance (P1, phase P3) | T532 | `MachinePresentationModel.cs`, `AttentionModel.cs`, `Presentation/World/`, `contracts/presentation-model.md` | WU13, WU15 | spec.md US4, FR-025–FR-031; research.md R08, R09; contracts/presentation-model.md §2–3; data-model.md MachinePresentation, AttentionState | `scripts/test-unity.ps1 -TestFile` for MachinePresentationModelTests, AttentionModelTests, AttentionDisplayTests (recompile first, total > 0); diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU30 | Phase 4: User Story 2 — A convincing industrial place (P1, phase P2, art first) | T528 | `DepotLinkView.cs`, `DepositView.cs`, `Presentation/World/` | WU28, WU29 | spec.md US2, FR-004, FR-010a–FR-015, FR-044; research.md R06, R07, R12, R16, R17, R23; contracts/world-presentation.md §1–5, contracts/presentation-model.md §4; docs/art/F004-art-bible-v1.md; docs/world/world-bible-v0.1.md; wiki/pages/art-production-pipeline.md, f006-world-presentation-design.md | `scripts/test-unity.ps1 -TestFile` for DepotLinkModelTests, DepotLinkViewTests, DepositViewTests (recompile first, total > 0); diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU31 | Phase 4: User Story 2 — A convincing industrial place (P1, phase P2, art first) | T529 | `EffectPool.cs`, `Presentation/World/` | WU27, WU29 | spec.md US2, FR-004, FR-010a–FR-015, FR-044; research.md R06, R07, R12, R16, R17, R23; contracts/world-presentation.md §1–5, contracts/presentation-model.md §4; docs/art/F004-art-bible-v1.md; docs/world/world-bible-v0.1.md; wiki/pages/art-production-pipeline.md, f006-world-presentation-design.md | `scripts/test-unity.ps1 -TestFile` for EffectPoolTests, F006AllocationTests (recompile first, total > 0); diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU32 | Phase 4: User Story 2 — A convincing industrial place (P1, phase P2, art first) | T530 | Parent-only. `tools/art-pipeline/trials/timed_trial.py`, `specs/006-world-first-factory-experience/reviews/readability-f006.md` | WU28, WU30, WU31 | spec.md US2, FR-004, FR-010a–FR-015, FR-044; research.md R06, R07, R12, R16, R17, R23; contracts/world-presentation.md §1–5, contracts/presentation-model.md §4; docs/art/F004-art-bible-v1.md; docs/world/world-bible-v0.1.md; wiki/pages/art-production-pipeline.md, f006-world-presentation-design.md | Parent records the decision or gate result with date and evidence links; diff limited to the named records. |
| WU33 | Phase 4: User Story 2 — A convincing industrial place (P1, phase P2, art first) | T531 | `Assets/IdleFactory/Tests/PlayMode/Fixtures/W006V/`, `specs/006-world-first-factory-experience/reviews/performance-b4.md` | WU28, WU30, WU31 | spec.md US2, FR-004, FR-010a–FR-015, FR-044; research.md R06, R07, R12, R16, R17, R23; contracts/world-presentation.md §1–5, contracts/presentation-model.md §4; docs/art/F004-art-bible-v1.md; docs/world/world-bible-v0.1.md; wiki/pages/art-production-pipeline.md, f006-world-presentation-design.md | `scripts/check-settings-noise.ps1 -Commit <sha>`; diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU34 | Phase 6: User Story 3 — Inspect and act on a machine in place (P1, phase P3) | T533 | `Assets/IdleFactory/Unity/UI/PanelScaffold.cs`, `.uxml`, `.uss` | WU12 | spec.md US3, FR-016–FR-019; research.md R10, R11; contracts/ui-surfaces-f006.md §2, §4; wiki/pages/run-panel-conventions.md, ui-toolkit-hud-layout-pitfalls.md, run-shell-device-profile-and-text-scale.md | `scripts/test-unity.ps1 -TestFile` for PanelScaffoldTests (recompile first, total > 0); `scripts/check-settings-noise.ps1 -Commit <sha>`; diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU35 | Phase 6: User Story 3 — Inspect and act on a machine in place (P1, phase P3) | T534 | `Assets/IdleFactory/Unity/UI/MachineCard.cs`, `.uxml`, `.uss`, `Panels/MachinePanelPresenter.cs`, `test-rebaseline.md` | WU13, WU29, WU34 | spec.md US3, FR-016–FR-019; research.md R10, R11; contracts/ui-surfaces-f006.md §2, §4; wiki/pages/run-panel-conventions.md, ui-toolkit-hud-layout-pitfalls.md, run-shell-device-profile-and-text-scale.md | `scripts/test-unity.ps1 -TestFile` for MachineCardTests, MachineDetailsParityTests, CardCameraTests, KeyboardTraversalTests (recompile first, total > 0); `scripts/check-settings-noise.ps1 -Commit <sha>`; diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU36 | Phase 7: User Story 6 — All game systems in one coherent, reachable structure (P1, phase P3) | T535 | `Assets/IdleFactory/Unity/UI/Panels/HubPanel.cs`, `.uxml`, `.uss`, `test-rebaseline.md` | WU34, WU35 | spec.md US6, FR-020–FR-024; research.md R11; contracts/ui-surfaces-f006.md §3–4; wiki/pages/run-panels.md, run-panel-conventions.md | `scripts/test-unity.ps1 -TestFile` for HubPanelTests, BackOrderTests, AndroidBackGestureTests (recompile first, total > 0); `scripts/check-settings-noise.ps1 -Commit <sha>`; diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU37 | Phase 7: User Story 6 — All game systems in one coherent, reachable structure (P1, phase P3) | T536 | `Assets/IdleFactory/Unity/UI/Panels/`, `Assets/IdleFactory/Unity/UI/RunShellDocument.cs`, `Assets/IdleFactory/Tests/PlayMode/F006/SurfaceConsistencyTests.cs` | WU36 | spec.md US6, FR-020–FR-024; research.md R11; contracts/ui-surfaces-f006.md §3–4; wiki/pages/run-panels.md, run-panel-conventions.md | `scripts/test-unity.ps1 -TestFile` for SurfaceConsistencyTests, ReducedMotionTests (recompile first, total > 0); `scripts/check-settings-noise.ps1 -Commit <sha>`; diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU38 | Phase 8: User Story 1 — The factory is the screen (P1, phase P3) | T538 | `Assets/IdleFactory/Unity/Persistence/DisplayPreferencesStore.cs`, `display-v2.json` | WU01 | spec.md US1, FR-003, FR-007–FR-010, FR-008a; research.md R11, R20; contracts/ui-surfaces-f006.md §1; data-model.md DisplayPreferences v2; wiki/pages/f004-display-preferences-store.md, ui-toolkit-hud-layout-pitfalls.md | `scripts/test-unity.ps1 -TestFile` for DisplayPreferencesV2Tests, DisplayPreferencesStoreTests (recompile first, total > 0); `scripts/check-settings-noise.ps1 -Commit <sha>`; diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU39 | Phase 8: User Story 1 — The factory is the screen (P1, phase P3) | T537 | `Assets/IdleFactory/Unity/UI/FactoryRun.uxml`, `.uss`, `RunShellDocument.cs`, `contracts/ui-surfaces-f006.md`, `world-copy.md`, `reviews/baseline.md` | WU36, WU38 | spec.md US1, FR-003, FR-007–FR-010, FR-008a; research.md R11, R20; contracts/ui-surfaces-f006.md §1; data-model.md DisplayPreferences v2; wiki/pages/f004-display-preferences-store.md, ui-toolkit-hud-layout-pitfalls.md | `scripts/test-unity.ps1 -TestFile` for PersistentLayerTests, RunHudLayoutTests, RunKeyboardFocusTests, RunTextScaleTests, NavigationInputTests, RunDeviceProfileTests (recompile first, total > 0); `scripts/check-settings-noise.ps1 -Commit <sha>`; diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU40 | Phase 9: User Story 5 — Building and progress feel rewarding (P2, phase P3) | T539 | `Assets/IdleFactory/Unity/UI/Panels/BuildCard.cs`, `.uxml`, `.uss` | WU35 | spec.md US5, FR-032–FR-036; research.md R13, R14, R15; contracts/ui-surfaces-f006.md §5, contracts/presentation-model.md §5–6, contracts/world-presentation.md §3 | `scripts/test-unity.ps1 -TestFile` for BuildCardTests, BuildSheetPanelTests (recompile first, total > 0); `scripts/check-settings-noise.ps1 -Commit <sha>`; diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU41 | Phase 9: User Story 5 — Building and progress feel rewarding (P2, phase P3) | T540 | `DevelopmentStage.cs`, `MilestoneWatcher.cs`, `Assets/IdleFactory/Unity/UI/MilestoneBanner.cs`, `world-copy.md` | WU28, WU30, WU34, WU37 | spec.md US5, FR-032–FR-036; research.md R13, R14, R15; contracts/ui-surfaces-f006.md §5, contracts/presentation-model.md §5–6, contracts/world-presentation.md §3 | `scripts/test-unity.ps1 -TestFile` for DevelopmentStageTests, StageTriptychDeltaTests, MilestoneWatcherTests, MilestoneBannerTests (recompile first, total > 0); `scripts/check-settings-noise.ps1 -Commit <sha>`; diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU42 | Phase 10: Verification and evidence (phase P4) | T541 | `scripts/android-emulator-journey.ps1` | WU39 | spec.md Success Criteria; research.md R16, R18, R19; quickstart.md; reviews/baseline.md; wiki/pages/f005-batch-d-journeys.md, android-emulator-validation.md, unity-player-validation.md | diff inspection against the listed paths plus the closest validation named in the task; diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU43 | Phase 10: Verification and evidence (phase P4) | T542 | `scripts/test-art-pipeline.ps1`, `scripts/android-emulator-journey.ps1`, `reviews/baseline.md`, `reviews/final-test-evidence.md`, `test-rebaseline.md` | WU37, WU39, WU38, WU40, WU41, WU42 | spec.md Success Criteria; research.md R16, R18, R19; quickstart.md; reviews/baseline.md; wiki/pages/f005-batch-d-journeys.md, android-emulator-validation.md, unity-player-validation.md | `powershell -NoProfile -File scripts/test-art-pipeline.ps1` exit 0 (3 = blocker); diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU44 | Phase 10: Verification and evidence (phase P4) | T543 | `specs/006-world-first-factory-experience/reviews/` | WU35, WU39, WU41, WU43 | spec.md Success Criteria; research.md R16, R18, R19; quickstart.md; reviews/baseline.md; wiki/pages/f005-batch-d-journeys.md, android-emulator-validation.md, unity-player-validation.md | diff inspection against the listed paths plus the closest validation named in the task; diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU45 | Phase 10: Verification and evidence (phase P4) | T544 | `reviews/performance-b4.md` | WU43 | spec.md Success Criteria; research.md R16, R18, R19; quickstart.md; reviews/baseline.md; wiki/pages/f005-batch-d-journeys.md, android-emulator-validation.md, unity-player-validation.md | diff inspection against the listed paths plus the closest validation named in the task; diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU46 | Phase 10: Verification and evidence (phase P4) | T545 | `tools/art-pipeline/trials/f006/` | WU39, WU41 | spec.md Success Criteria; research.md R16, R18, R19; quickstart.md; reviews/baseline.md; wiki/pages/f005-batch-d-journeys.md, android-emulator-validation.md, unity-player-validation.md | `powershell -NoProfile -File scripts/test-art-pipeline.ps1` exit 0 (3 = blocker); diff limited to the listed paths; independent QA PASS on the exact commit. |
| WU47 | Phase 10: Verification and evidence (phase P4) | T546 | Parent-only. `reviews/readability-f006.md` | WU46 | spec.md Success Criteria; research.md R16, R18, R19; quickstart.md; reviews/baseline.md; wiki/pages/f005-batch-d-journeys.md, android-emulator-validation.md, unity-player-validation.md | Parent records the decision or gate result with date and evidence links; diff limited to the named records. |
| WU48 | Phase 10: Verification and evidence (phase P4) | T547 | Read-only review. `reviews/world-bible-conformance.md` | WU39, WU41 | spec.md Success Criteria; research.md R16, R18, R19; quickstart.md; reviews/baseline.md; wiki/pages/f005-batch-d-journeys.md, android-emulator-validation.md, unity-player-validation.md | Review record exists at the named path with verdicts and findings; the reviewer is a fresh session and not the producer; no code diff. |
| WU49 | Phase 10: Verification and evidence (phase P4) | T548 | Read-only review. `specs/006-world-first-factory-experience/reviews/final-acceptance.md` | WU43, WU44, WU45, WU46, WU47, WU48 | spec.md Success Criteria; research.md R16, R18, R19; quickstart.md; reviews/baseline.md; wiki/pages/f005-batch-d-journeys.md, android-emulator-validation.md, unity-player-validation.md | Review record exists at the named path with verdicts and findings; the reviewer is a fresh session and not the producer; no code diff. |
| WU50 | Phase 10: Verification and evidence (phase P4) | T549 | Parent-only. `reviews/final-acceptance.md` | WU49 | spec.md Success Criteria; research.md R16, R18, R19; quickstart.md; reviews/baseline.md; wiki/pages/f005-batch-d-journeys.md, android-emulator-validation.md, unity-player-validation.md | Parent records the decision or gate result with date and evidence links; diff limited to the named records. |
| WU51 | Phase 11: Closeout hooks and convergence (parent) | T550 | Parent-only. `specs/006-world-first-factory-experience/tasks.md`, `specs/006-world-first-factory-experience/learn.md`, `wiki/` | WU50 | AGENTS.md (MAQA lane, after_implement hooks); quickstart.md | Parent records the decision or gate result with date and evidence links; diff limited to the named records. |
| WU52 | Phase 11: Closeout hooks and convergence (parent) | T551 | Parent-only. `scripts/maqa-stale-worktrees.ps1` | WU51 | AGENTS.md (MAQA lane, after_implement hooks); quickstart.md | Parent records the decision or gate result with date and evidence links; diff limited to the named records. |

## Phase 1: Setup and gates

- [X] T500 *Parent* (done 2026-11-11 at `2ae1eee`; see `plan.md` › Implementation lane): Verify the gates on the exact base commit:
  - F004 is complete on `main` (77/77 tasks, T447 and T449).
  - The constitution, branch convention, wiki, `maqa` labels including `resource:unity|emulator|gpu`, and the `maqa-config.yml` commands are in place.
  - `$speckit-analyze` reports no unresolved critical finding.
  - The orchestration ledger validates for `specs/006-world-first-factory-experience/tasks.md`.

  Record the implementation-lane decision (MAQA proposed) and the activated and omitted roles in `plan.md`. Ask the owner to confirm **OD-11**, the revised `CompletedCycles` design from R08, and record the answer in `plan.md`. Resources: none. Covers FR-045.

  **P3 batch boundaries**, used by T543:
  - Batch A: T532, T533, T534.
  - Batch B: T535, T536, T537, T538.
  - Batch C: T539, T540.
- [X] T501 *Parent*: Discovery and plan review waves are recorded in `reviews/discovery.md` and `reviews/plan-reviews.md`, with every disposition applied in revision 2 (done 2026-11-11). Resources: none.

## Phase 2: User Story 8 (direction gate) — Baseline, World Bible and mockups (P1, phase P0)

**Goal**: The owner approves a concrete world setting and visual direction from rendered evidence before production starts.

**Independent test**: `reviews/baseline/` holds probe JSON per screen class. `docs/world/world-bible-v0.1.md` is approved. `reviews/direction.md` holds at least 2 directions with all sheets and records the owner decision.

- [X] T502 [US8] Test/QA (UI/UX lens).
  - **Build**: `ShellGeometryProbe` in `Assets/IdleFactory/Tests/PlayMode/F006/ShellGeometryProbe.cs`, with a self-test on synthetic layouts in `ShellGeometryProbeTests.cs`. The probe computes:
    - the world rect from `RunShellDocument.TryGetUnobstructedScreenRect`;
    - the chrome union of the persistent elements' `worldBound` within the safe area;
    - the count of visible `Label`s with text.
  - **Journey**: a deterministic interaction-count journey, `InteractionCountJourney.cs`. It dispatches panel events and `Click` through the real shell; device pointer events are not used in batch mode. It counts:
    - upgrade and return;
    - diagnose one machine;
    - open and leave each system;
    - build in an empty slot.
  - **Baseline**: run the probe and capture the screenshot matrix on the **current** build for every `ShellScreens.All()` class plus the emulator at 1080x2400 in both orientations. States: no panel, machine sheet, Contracts, build sheet, offline summary. Text scales: 100 % and 200 %.
  - **Output**: `specs/006-world-first-factory-experience/reviews/baseline/<class>.json`, the PNGs, and `reviews/baseline.md`.
  - **Validation**: the probe self-test passes; the JSON is reproducible on two runs.
  - **Resources**: unity, emulator.
  - **Covers**: FR-046, SC-001, SC-002, SC-003, SC-010; research R02.
- [X] T503 [P] [US8] World Designer (Art Director and UI/UX lenses, read-only to code).
  - **Write** `docs/world/world-bible-v0.1.md`, at most about 2 pages, containing:
    - the site concept: a neutral starter industrial platform on a dark base;
    - the site name and tagline;
    - mood words;
    - naming and copy rules, including a tone vocabulary and forbidden terms ("Systems" as the hub label);
    - the swappable-ground rule (a ground family keyed by region id, with one region today);
    - deposit notes for later regions;
    - the version and semver rules.
  - **Write** `specs/006-world-first-factory-experience/reviews/world-copy.md`. List each world-facing string with its owner, the World Bible version, and a fit note at 200 %. Strings: 2–3 hub label candidates, milestone banner texts, stage names, site name, depot and vein names if shown.
  - **Validation**: Art Director and UI/UX lens read-through comments are appended.
  - **Resources**: none.
  - **Covers**: FR-047; research R22.
- [X] T504 *Parent* after T503 (done 2026-11-11, c95581f): Record the owner decision **OD-9a**, which approves the World Bible v0.1 text. Freeze the version in the header of `docs/world/world-bible-v0.1.md` and record any wording changes in `specs/006-world-first-factory-experience/reviews/world-copy.md`. No direction brief starts before this.
  - **Covers**: FR-047, OD-9a, OD-15.
- [X] T505 [P] [US8] Technical Artist (Test/QA lens).
  - **Exporter**: Edit Mode test `Assets/IdleFactory/Tests/EditMode/F006/LatticeGeometryExportTests.cs`. It exports today's `SlotGridLayout` geometry (pivots, plate rects, Extent, hit diamonds, row gaps, and `CameraScaleModel` n_min and n_fit) per screen class and orientation to `tools/art-pipeline/f006/lattice-geometry.json`.
  - **Compositor**: a `mockup` command in `tools/art-pipeline` (Pillow, pinned in `uv.lock`). It reads that JSON and derives:
    - deck cells, including the margin band;
    - row trunks and per-location branches (R07);
    - depot and deposit cells from parameters that mirror `WorldCompositionSettings` (R22).

    It composites sprites at exact device pixels per screen class. It draws the UI from a layout description in points, using Art Bible fonts and tokens.
  - **Inputs**: the palette and `check_flash` gates run on the inputs.
  - **Output**: PNG sheets, plus 6-frame GIF/APNG strips.
  - **Validation**: pytest for deterministic output (byte-identical across two runs), for lane geometry on 3×4, 4×3, 6, 20 and L test layouts given as explicit cells, and for keep-out.
  - **Resources**: unity (Edit Mode export only).
  - **Covers**: FR-040, FR-041; research R03, R07.
- [X] T506 [US8] Art Director with Asset Artist (World Designer lens), after T504.
  - **Briefs**: at least 2 direction briefs in `art-source/f006/briefs/direction-<a|b>.md`. Default candidates: C, a platform deck, and one alternative (A, a walled hall, or a C variant). Each brief cites World Bible v0.1 and Art Bible v1.0.4 and states palette ids only (no new hue).
  - **Concept sprites**: produced through the Blender rig-v2 or the scripted route (no ComfyUI final art):
    - deck, hatch and edge tiles;
    - depot;
    - vein;
    - 2 props;
    - branch and pip;
    - completion pop.

    Store them under `art-source/f006/concept/` with provenance stubs.
  - **Validation**: `powershell -NoProfile -File scripts/test-art-pipeline.ps1` exits 0. Exit code 3 (tooling unavailable) is a blocker.
  - **Resources**: gpu.
  - **Covers**: FR-040, FR-044, FR-047; research R03, R17.
- [X] T507 [US8] Asset Artist (UI/UX lens), after T505 and T506.
  - **Run** the `mockup` compositor for each direction. Output to `specs/006-world-first-factory-experience/reviews/direction/`.
  - **Screen classes**:
    - phone portrait 1080x2400;
    - phone landscape 2400x1080;
    - a 640x360-class sheet at 200 % text;
    - tablet 1536x2048.
  - **Each sheet shows**:
    - an active factory with at least 8 machines in mixed states;
    - the depot, trunks and branches;
    - the vein;
    - the docked machine card next to its selected machine, with framing;
    - the hub open, with its text label;
    - the stage triptych (early, partial, established);
    - a 200 % variant.
  - **Animation**: 6-frame strips (machine loop, branch pip, completion pop) and a reduced-motion still.
  - **Validation**: the pipeline checks run on the inputs; the sheet manifest is complete.
  - **Resources**: none.
  - **Covers**: FR-040, FR-041; research R03.
- [X] T508 [US8] After T507: independent read-only reviews by a UI/UX Reviewer and an Art Director in fresh sessions, neither of them the producer, plus a World Designer conformance read. They compare the directions against `Core-Idea.md`, the spec user stories and World Bible v0.1. The result is a recommendation in `specs/006-world-first-factory-experience/reviews/direction.md`.
  - **Resources**: none.
  - **Covers**: FR-040, FR-045.
- [X] T509 *Parent* after T508 (done 2026-11-11, 4556ae1): Record the owner's decision **OD-9b** in `reviews/direction.md`:
  - the chosen direction;
  - the hub label wording from `world-copy.md`;
  - any changes.
  - **Covers**: FR-040, SC-012 (part 1).

## Phase 3: Foundational refactor and User Story 7 — Not bound to 12 (P1 enabling, US7 P2)

**Goal**: Pure-move refactors and the world-layout seam, with no behaviour change.

**Independent test**: All existing suites stay green. There is no scene diff. The 6, 20 and L test layouts pass through the real view, camera and selection (SC-011).

- [X] T510 Gameplay Developer (Software Architect and Test/QA lenses; Medium, high reasoning), after T500.
  - **Change**: a pure move plus a visibility change (R05).
    - `git mv Assets/IdleFactory/Unity/Presentation/SlotGridView.cs` together with its `.meta` (GUID kept) to `Assets/IdleFactory/Unity/Presentation/World/SlotGridView.cs`.
    - Extract `SlotVisualModel.cs` (`SlotCondition`, `SlotVisual`, `SlotGridModel`), `SlotLattice.cs` (`SlotGridLayout`), `FloorRange.cs`, `RunZoomSettings.cs` and `SlotRenderers.cs`. `SlotRenderers` changes from private nested to internal.
    - Keep namespaces, type names and serialized fields. Edit no test except `using` lines.
  - **Validation**:
    - full `scripts/test-unity.ps1` with the same pass set as the base;
    - no `.unity`, `.prefab` or `.asset` diff;
    - `scripts/check-settings-noise.ps1 -Commit <sha>`.
  - **Resources**: unity.
  - **Covers**: FR-037; research R05.
- [X] T511 [P] UI/UX Developer (Software Architect and Test/QA lenses; Medium, high reasoning), after T500.
  - **Change**: a pure move (R11).
    - Extract the back and sheet logic from `Assets/IdleFactory/Unity/UI/RunShellDocument.cs` into `Assets/IdleFactory/Unity/UI/ShellNavigationStack.cs` and `ShellSheetHost.cs`.
    - Keep `HandleBack` as a delegating method. `StartupStatusPanel.cs:667` still calls it.
    - Keep `ShellTab`, `SelectTab` and the tab bar unchanged as a shim.
    - The frame order matches today's behaviour (dialog, pinned value, settings, sheet).
  - **Tests**: `Assets/IdleFactory/Tests/EditMode/F006/ShellNavigationStackTests.cs`.
  - **Validation**: the existing Play Mode suites are unchanged and green, including `RunKeyboardFocusTests`, `RunHudLayoutTests` and `AndroidBackGestureTests`.
  - **Resources**: unity.
  - **Covers**: FR-021, FR-024; research R11.
- [X] T512 [US7] Gameplay Developer (Software Architect and Test/QA lenses; Medium, high reasoning), after T510 and T505.
  - **Layout seam** in `Assets/IdleFactory/Unity/Presentation/World/`: `FactoryWorldLayout.cs`, `LatticeLayoutBuilder.cs` and `BuildingLocationId.cs`, per `data-model.md` and `contracts/world-presentation.md` §4.
    - `Bounds` = today's `SlotGridLayout.Extent`, with `Margin` kept separate.
    - Members: `LayoutId`, `TryHit`, `DeckCells` over all locations, `DepotCell`, `DepositCell`, `TrunkOf` and `BranchOf`.
  - **Composition asset**: `WorldCompositionSettings.cs`, a ScriptableObject with the fields from `data-model.md`. Its asset `Assets/IdleFactory/Art/F006/WorldCompositionSettings.asset` is created through Unity tooling with defaults that reproduce F004.
  - **Migration**: `CameraScaleModel`, `RunCameraFraming` and `ScaleLayout` replace `Columns` and `Key` with `LayoutId` and `Bounds`. The callers in the bootstrap (`:852`, `:1183`) and in `RunShellDocument` (`:633`, `:672`) move along. `FactoryCameraController` stays the sole writer.
  - **Test layouts**: an explicit-cells constructor and a `TestLayouts` builder (6, 20, L) under `Assets/IdleFactory/Tests/*/F006/`.
  - **Tests**:
    - `WorldLayoutParityTests`: exact F004 geometry and `LargestFittingScale` per screen class; it also matches `tools/art-pipeline/f006/lattice-geometry.json` from T505.
    - `WorldLayoutValidationTests`.
    - `LayoutIndependenceTests` (Play Mode, real view, camera, hit-test and selection).
    - `NoFixedSlotCountGuardTests`, a `git grep` guard on new code that lists the existing 12-slot tests as exempt.
  - **Re-baseline**: record it in `reviews/test-rebaseline.md` for `SlotGridLatticeTests`, `SlotGridTests`, `CameraScaleModelTests` and `ScreenClassScaleTableTests`.
  - **Resources**: unity.
  - **Covers**: FR-037, FR-038, SC-011; research R04, R22.
- [X] T513 [P] Technical Artist (DevOps/Operations Low-tier lens; Test/QA lens), after T500.
  - **Change**: create the sorting layers `WorldBase`, `WorldDeck`, `WorldLinks` (before `Default`) and `Effects` (after `Overlay`) in `ProjectSettings/TagManager.asset` through Unity tooling.
  - **Guard test**: `Assets/IdleFactory/Tests/EditMode/F006/SortingLayerGuardTests.cs` fails when a layer is missing or the order changes. It accounts for `SortingLayer.NameToID` silently returning 0.
  - **Settings noise**: commit with `IDLEFACTORY_ALLOW_SETTINGS_NOISE=1` and record the decision in `reviews/test-rebaseline.md` › Settings. Run `scripts/check-settings-noise.ps1 -Commit <sha>`; it shows TagManager only.
  - **Resources**: unity.
  - **Covers**: FR-014; research R06.
- [X] T514 [P] Gameplay Developer (Software Architect and Game Systems/Economy lenses; Medium, high reasoning), after T500 (OD-11 confirmed).
  - **Change**: implement R08 and `contracts/presentation-model.md` §1.
    - `Assets/IdleFactory/Core/Run/Simulation/RunSimulator.cs` records a per-advance count per machine in `CompletionPass`: "+1 per completed cycle, also for a partial batch", "exposed only after a successful `Commit()`; a faulted advance exposes 0".
    - `Assets/IdleFactory/Core/Run/Application/RunSession.cs` accumulates a `long` per `InstanceId`: "pruned on removal, reset on replace, empty on construction, restore and prestige".
    - `MachineStateView.cs` gains `CompletedCycles` through an added constructor parameter with default `0`.
    - The value is not saved and is never read by economy, offline or prestige code.
  - **Tests** in `Assets/IdleFactory/Tests/EditMode/F006/`:
    - `CompletedCyclesLifecycleTests`: faulted advance, multiple completions, remove and replace, prestige, restore.
    - `CompletedCyclesPropertyTests`: the delta sum equals the engine-trace completions on W003 and W005, including batch, offline and restore.
    - `CompletedCyclesNoObserverEffectTests`: save and trace SHA are identical.
    - An allocation check of `CopyMachineStates` and `RefreshViews`.
  - **API fixture**: record the `core-public-api.txt` diff in `reviews/test-rebaseline.md`.
  - **Resources**: unity.
  - **Covers**: FR-001, FR-002, FR-029, SC-007; research R08.
- [X] T515 [P] Test/QA (UI/UX Developer lens), after T511.
  - **Helper**: add a `ShellTab`/`TabButton` compatibility helper to the Play Mode shell test support (`RunShellTestSupport` or its equivalent), so that the about 35 panel tests can switch from tab buttons to hub navigation in one place later.
  - **Re-baseline file**: create `specs/006-world-first-factory-experience/reviews/test-rebaseline.md`, with the full per-file list from R19 and an old→new table template.
  - **Validation**: the existing suites stay green.
  - **Resources**: unity.
  - **Covers**: SC-007; research R19.

## Phase 4: User Story 2 — A convincing industrial place (P1, phase P2, art first)

**Goal**: The approved world art, integrated on the layout seam.

**Independent test**: Before/after captures on every screen class. The SC-005 re-trial shows no regression. The World Designer conformance check finds zero conflicts.

- [X] T516 [US2] Art Director (Technical Artist and World Designer lenses), after T509.
  - **Draft** Art Bible v1.1.0 in `docs/art/F004-art-bible-v1.md`: a revision-log entry, marked "draft pending OD-10", and a header note.
  - **Validation**: `grep -n "v1.1.0" docs/art/F004-art-bible-v1.md` finds the header and the revision-log row. The palette check reports 48 ids.
  - **Amendments**: §2.2 (numeric world-layer budget per `contracts/world-presentation.md` §5), §5, §6.4, §6.5, §8.5, §9.4 and §12 (R17).
  - **Allowance table**: per family, with no new palette id. `lit_*` is not used for links, pips, sparks or lamps.
  - **Sprite table**: sprite by sprite, with padding and page rounding. The world atlas and the FX atlas are each at most 1024².
  - **Families**: GROUND, FOUNDATION, DEPOT (12 sprites), LINK, PROP, EDGE, DEPOSIT, FX and optional UI.
  - **Citations**: the draft cites World Bible v0.1 and OD-9.
  - **Resources**: none.
  - **Covers**: FR-004, FR-044; research R17.
- [X] T517 [P] [US2] Asset Artist (Art Director and UI/UX lenses), after T509.
  - **Measure** the A-03 badge-anchor candidates (x = 40 centred; a lower or left offset; a pointer tail) against:
    - the 54 px row band;
    - the selection band;
    - the overlap proof;
    - 23 px legibility at n_min.
  - **Render** candidate sheets per screen class into `specs/006-world-first-factory-experience/reviews/a03-anchor/`, with the measurement table and a recommendation in `reviews/a03-anchor.md`.
  - **Validation**: the measurement script runs under pytest in `tools/art-pipeline`.
  - **Resources**: none.
  - **Covers**: FR-014, FR-025; research R17.
- [X] T518 *Parent* after T516 and T517 (done 2026-11-11, e6c2d2a): Record the owner decisions **OD-12** (A-03 anchor) and **OD-10** (Art Bible v1.1.0 sign-off, including the world-layer budget and the texture table) in the Art Bible revision log and in `plan.md`.
  - **Covers**: FR-004.
- [X] T519 [P] [US2] 3D and Render Artist (Technical Artist lens), after T518.
  - **Family**: F006-GROUND (deck, base, hatch with the §8.5 padlock and digit kept, and edge/corner/T tiles per the OD-9 edge style), plus F006-FOUNDATION (flat outline and solid 70×35 diamonds, no plinth wall).
  - **Route**: Blender rig-v2, then the deterministic pixel pass and provenance.
  - **Paths**: `art-source/f006/ground/` and `art-source/f006/foundation/`.
  - **Validation**: `scripts/test-art-pipeline.ps1` exits 0.
  - **Resources**: gpu.
  - **Covers**: FR-011, FR-012, FR-044; research R06, R17.
- [X] T520 [P] [US2] 3D and Render Artist (Technical Artist lens), after T518.
  - **Family**: F006-DEPOT (4 fill states × 3 stage variants = 12 sprites) and F006-DEPOSIT (vein plus a 6-frame chip loop).
  - **Route**: Blender rig-v2, then the pixel pass and provenance.
  - **Paths**: `art-source/f006/depot/` and `art-source/f006/deposit/`.
  - **Validation**: the pipeline exits 0, including `check_flash` on the chip loop.
  - **Resources**: gpu.
  - **Covers**: FR-010a, FR-013, FR-044; research R07, R23.
- [X] T521 [P] [US2] 3D and Render Artist (Technical Artist and World Designer lenses), after T518.
  - **Family**: F006-PROP (props at most 24 art px tall; static lamps in steel or teal, never `lit_*`) and F006-EDGE (railing, wall and gantry per stage).
  - **Route**: Blender rig-v2.
  - **Paths**: `art-source/f006/prop/` and `art-source/f006/edge/`.
  - **Validation**: the pipeline exits 0.
  - **Resources**: gpu.
  - **Covers**: FR-011, FR-015, FR-036, FR-044; research R06, R15.
- [X] T522 [P] [US2] Asset Artist (Art Director lens), after T518.
  - **Family**: F006-LINK (trunk, branch, active branch, pip) and F006-FX:
    - completion pop, spark, steam, build dust and upgrade glint, each 6 frames;
    - a static reduced-motion highlight.
  - **Route**: scripted.
  - **Paths**: `art-source/f006/link/` and `art-source/f006/fx/`.
  - **Validation**:
    - the pipeline exits 0;
    - `check_flash` passes on body + accent + FX composites for all four machine families at 6, 8 and 10 fps, recording the per-family pace cap;
    - the Assembler is capped at 8 fps or lower.
  - **Resources**: none.
  - **Covers**: FR-013, FR-015, FR-029, FR-034, FR-044; research R07, R12.
- [X] T523 [P] [US2] Asset Artist (UI/UX lens), after T518.
  - **Family**: F006-UI sprites, only where UI Toolkit styling cannot express the hub entry icon, the attention indicator or the banner frame. Otherwise record "not needed" in the Art Bible.
  - **Route**: scripted.
  - **Path**: `art-source/f006/ui/`. Record the "needed" or "not needed" decision per element in the Art Bible v1.1 family table.
  - **Validation**: `scripts/test-art-pipeline.ps1` exits 0 when any sprite is produced.
  - **Resources**: none.
  - **Covers**: FR-020, FR-023, FR-044.
- [X] T524 [P] [US2] Asset Artist (Art Director lens), after T518.
  - **Change**: apply the OD-12 anchor to the cue overlay geometry in `Assets/IdleFactory/Unity/Presentation/StateCueSet.cs` (`StateCueGeometry`) and to the cue sheets under `art-source/f006/`. Re-baseline `Assets/IdleFactory/Tests/EditMode/StateCueSetTests.cs`.
  - **Re-run**: the SC-001 sheet generation inputs.
  - **Validation**: the pipeline exits 0 and the `StateCueSetTests` re-baseline is recorded.
  - **Resources**: unity, because of the `StateCueSetTests` re-baseline.
  - **Covers**: FR-014, FR-025; research R17.
- [ ] T525 [US2] Independent read-only reviews per family, after T519–T524:
  - Art Director in a fresh session, not the producer;
  - World Designer conformance;
  - UI/UX (cue legibility on the new backdrop);
  - Game Designer/PO (readability).

  Record them in `specs/006-world-first-factory-experience/reviews/art-direction-f006.md` and `reviews/world-bible-conformance.md` (part 1). The Game Systems/Economy Low-tier lens confirms "no pacing effect" for the vein.
  - **Resources**: none.
  - **Covers**: FR-044, FR-047, SC-013; research R17, R23.
- [ ] T526 [US2] Technical Artist (Art Director and Performance lenses), after T525 and T513.
  - **Import**: approved families into `Assets/IdleFactory/Art/F006/` through the manifest:
    - one world atlas holding the static families;
    - one FX atlas;
    - PPU 24, point filtering, no compression on world atlases;
    - catalog keys in the `PresentationSpriteCatalog` registry.
  - **Tests**: `Assets/IdleFactory/Tests/EditMode/F006/F006AssetContractTests.cs` (import settings, atlas membership and pages, sprite counts, keys).
  - **Validation**: `scripts/test-art-pipeline.ps1` exits 0 and `scripts/test-unity.ps1` passes.
  - **Resources**: unity.
  - **Covers**: FR-004, FR-044, SC-008; research R06, R17.
- [ ] T527 [US2] Gameplay Developer (Technical Artist and Performance lenses), after T512 and T526.
  - **View**: `Assets/IdleFactory/Unity/Presentation/World/FactoryDeckView.cs`.
    - A **Rectangle** `Grid` with cells of 70×54 art px (cellSize 70/24 × 54/24) and Tilemaps on `WorldBase` and `WorldDeck`.
    - It covers all `DeckCells`: deck or hatch art per unlock, with the padlock and digit on the hatch.
    - It **replaces** the F004 floor pool.
  - **Props**: placed by the R06 mask, at most 24 art px, by cell hash and stage weights from `WorldCompositionSettings`.
  - **Scene wiring**: through Unity tooling only, with the slot sprite material.
  - **Tests**:
    - `DeckLayoutTests` (cells, prop mask with free-area measurement, seam test);
    - `DeckRenderTests` (Play Mode: sorting, no prop above the overlay, material parity);
    - `ReducedMotionWorldTests` (zero moving decorative pixels over 120 frames);
    - a `[Category("StandalonePlayer")]` Tilemap smoke test.
  - **Validation**: an Android IL2CPP build check for the tilemap module.
  - **Resources**: unity, emulator.
  - **Covers**: FR-011, FR-012, FR-014, FR-015, FR-037; research R06.
- [ ] T528 [US2] Gameplay Developer (Technical Artist lens), after T527 and T532, which provides `ActivityPace` and the Running state per machine.
  - **Views**: `DepotLinkView.cs` and `DepositView.cs` in `Presentation/World/`.
    - Trunks are static.
    - Branches belong to occupied locations only.
    - Pips run **on branches only** while the machine is Running, at 800, 600 or 400 ms per cell by pace.
    - The depot shows its fill index `min(3, floor(4·fill/capacity))` plus full, × stage.
    - The vein chip loop plays while manual mining is in progress. The vein is not a hit target.
    - Under reduced motion there are no pips and no chip loop; the static variants show instead.
  - **Tests**:
    - `DepotLinkModelTests` (geometry on 3×4, 4×3, 6, 20 and L; fill; branch presence; pace);
    - `DepotLinkViewTests` (a pip stops within 1 frame of a block);
    - `DepositViewTests` (placement on every test layout, mining-state follow, tap selects nothing).
  - **Resources**: unity.
  - **Covers**: FR-010a, FR-013, FR-015, FR-029; research R07, R23.
- [ ] T529 [US2] Gameplay Developer (Performance and Test/QA lenses), after T526 and T532.
  - **Wiring**: wire the pool to the model. `MachinePresentation.CompletionsDelta` spawns a CompletionPop. A baseline reset and offline deltas spawn nothing.
  - **Pool**: `EffectPool.cs` in `Presentation/World/`.
    - 8 renderers: 3 active and 5 draining tails.
    - Priority: CompletionPop outranks the decorative effects.
    - Coalescing: 250 ms per location, with timestamps sized from `LocationCount`.
  - **Spawning rules**:
    - no spawn under reduced motion or for offline deltas;
    - the per-family pace cap from T522;
    - a pace change takes effect at the next frame 0.
  - **Tests**: `EffectPoolTests` (Edit Mode) and `F006AllocationTests` (Play Mode and `[Category("StandalonePlayer")]`: clock tick, effect spawn and coalesce, with 0 steady-state allocations).
  - **Resources**: unity, emulator.
  - **Covers**: FR-006, FR-029, FR-034; research R12.
- [ ] T530 *Parent* after T527–T529 merged: Run the SC-005 re-trial on the new backdrop.
  - **Blind naming**: an independent agent session names cues in grayscale and three CVD filters, including §9.4 and portrait 720x1280 at n_min 2.
  - **Timed trial**: the owner runs the trial with `tools/art-pipeline/trials/timed_trial.py` using committed seeds and the oracle.
  - **Record**: `specs/006-world-first-factory-experience/reviews/readability-f006.md`.
  - **Covers**: SC-005; research R18.
- [ ] T531 [US2] Performance Engineer, after T527–T529.
  - **Workload**: define the W006-V fixture under `Assets/IdleFactory/Tests/PlayMode/Fixtures/W006V/`:
    - W004-V;
    - the established-stage deck;
    - 12 branches with pips;
    - the vein;
    - 3 effects;
    - the card open state stubbed until T534.
  - **Measure B4a** with the F004 method:
    - budgets #4, #5, #6, #7, #8 (SetPass delta per atlas), #10 and #11;
    - the 600-frame Windows-player allocation run.
  - **Record**: `specs/006-world-first-factory-experience/reviews/performance-b4.md`.
  - **Resources**: unity, emulator, gpu.
  - **Covers**: FR-006, SC-008; research R16.

## Phase 5: User Story 4 — Read the factory's health at a glance (P1, phase P3)

**Goal**: Derived state, upgrade availability, shared causes and the "short on" signal, all visible in the world.

**Independent test**: Model tests, plus the SC-004 trial on seeded captures (T546).

- [X] T532 [US4] Gameplay Developer (Software Architect, Game Systems/Economy Low-tier and Test/QA lenses), after T514 and T512. This task runs in P2, before T528 and T529, which consume its model. Attention marker positions come from `FactoryWorldLayout` (trunk junction, depot cell), not from the depot view.
  - **Multiple causes**: one world marker per shortage resource, at most 2 visible, ranked by the shortage order. The strip indicator shows the top resource plus "+N" for the others. Every marker shows its resource icon.
  - **No numbers in the world** (FR-030): no rate or efficiency numbers appear in the world. A test asserts that no world-space text element shows a number.
  - **Models**: `MachinePresentationModel.cs` and `AttentionModel.cs` in `Presentation/World/`, per `contracts/presentation-model.md` §2–3.
  - **Visual state**: comes from `SlotGridModel.StateFor`, the single precedence.
  - **Upgrade marker**: hidden under problem badges, never red, and smaller than a badge.
  - **Shortage**: "resources that block at least one MissingInput machine and have `HasDemand`", ordered by "coverage ascending, then affected count descending, then content order". No MissingInput machine means no signal. Copy: "Short on <resource>".
  - **Production attention**: one world marker on the `Overlay` layer, at the trunk junction or the depot. A tap selects the first affected location.
  - **Buffers**: reused, sized from `LocationCount`. Refresh only on a revision change.
  - **Tests**:
    - `MachinePresentationModelTests`;
    - `AttentionModelTests` (G-2 and G-3 mismatch cases, ties, no demand, store full, production versus system split, two or three simultaneous shortage resources);
    - `AttentionDisplayTests` (Play Mode).
  - **Resources**: unity.
  - **Covers**: FR-025–FR-031; research R08, R09.

## Phase 6: User Story 3 — Inspect and act on a machine in place (P1, phase P3)

**Goal**: A docked machine card with in-place upgrade and details, without losing sight of the machine.

**Independent test**: `MachineCardTests` and `CardCameraTests` on every class at 100, 150 and 200 %, plus the SC-003 journey counts.

- [X] T533 [US3] UI/UX Developer (Art Director and Test/QA lenses), after T511.
  - **Scaffold**: `Assets/IdleFactory/Unity/UI/PanelScaffold.cs`, `.uxml` and `.uss`.
    - A header with back.
    - Primary and secondary button classes.
    - One disabled state, conveyed by shape, text and contrast, not by dimming alone.
    - Empty, loading and error templates.
    - Notices layered above sheets.
  - **Governance**: Art Bible §11. Font and size tokens only.
  - **Tests**: `PanelScaffoldTests` (Play Mode, every class at 100, 150 and 200 %).
  - **Resources**: unity.
  - **Covers**: FR-005, FR-023; research R11.
- [ ] T534 [US3] UI/UX Developer (Software Architect, Test/QA and UI/UX Reviewer later lenses), after T512, T532 and T533.
  - **Card**: `Assets/IdleFactory/Unity/UI/MachineCard.cs`, `.uxml` and `.uss`. One instance, **docked only**:
    - phone portrait: a bottom band, "≤ 30 % at 100–140 % text, ≤ 45 % hard cap";
    - phone landscape: a side column of about 40 % width, Upgrade pinned;
    - tablet and desktop: a side panel.
  - **First view**: name and level, the state line, and Upgrade with cost and shortfall inside the button. Below it: the cause line with the resource icon and the Details chevron.
  - **Behaviour**:
    - Upgrade happens in place; focus stays on Upgrade.
    - A successful upgrade spawns the UpgradeGlint, and switch-on gives a restrained response, both through `EffectPool`. Under reduced motion each becomes a static 1 s highlight (FR-034). Covered by a `MachineCardTests` case.
    - A stale action shows an inline line.
    - A removed machine closes the card with a notice.
  - **Camera**: framing stays selection-driven via `OpenSheet(band, footprint)` and `CloseSheet`, with the band known before the reframe.
  - **Details**: reorganize `Panels/MachinePanelPresenter.cs` into the details view, with Remove behind `RequestConfirmation`.
  - **Keyboard order**: persistent layer, then locations by `SortKeyOf`, then the card. Focus returns to the location.
  - **Tests**:
    - `MachineCardTests` (Upgrade visible without scrolling at 200 % with worst-case copy on every class, including 640×360);
    - `MachineDetailsParityTests` (every F003–F005 panel capability is reachable);
    - `CardCameraTests` (dock, restore, rotation);
    - `KeyboardTraversalTests`;
    - `[Category("StandalonePlayer")]` card open and close.
  - **Re-baseline**: record the affected panel tests in `test-rebaseline.md`.
  - **Resources**: unity, emulator.
  - **Covers**: FR-016–FR-019, FR-024, SC-002, SC-003; research R10.

## Phase 7: User Story 6 — All game systems in one coherent, reachable structure (P1, phase P3)

**Goal**: A hub with labelled entry and attention, every system reachable and consistent.

**Independent test**: `HubPanelTests`, `BackOrderTests` and `SurfaceConsistencyTests`, plus the journey counts.

- [ ] T535 [US6] UI/UX Developer (Software Architect and Test/QA lenses), after T533 and T534.
  - **Hub**: `Assets/IdleFactory/Unity/UI/Panels/HubPanel.cs`, `.uxml` and `.uss` on `PanelScaffold`.
  - **Cards**: Market, Contracts, Items, Research (the RP value in its status), Industry, Overdrive, Prestige and Settings.
    - Industry gets an additive `ShellPanelIds` id that hosts `IndustryPanel`, with `IsKnown` updated.
    - Attention flags only on Contracts, Research, Prestige and Overdrive.
    - `ContractsShellFeed` is re-pointed to the Contracts card flag.
  - **Navigation**:
    - System views open through `ShellPanelRegistry` unchanged.
    - Return to the factory takes at most 1 interaction.
    - Opening the hub closes the machine card or build card and restores the camera.
    - Phone portrait uses a bottom sheet; the other classes use a side panel.
  - **Tests**:
    - `HubPanelTests` (every registered panel has a card; flags);
    - `BackOrderTests` (mixed sequences; Escape and Android Back; rotation);
    - the re-baselined `AndroidBackGestureTests`;
    - `[Category("StandalonePlayer")]` hub and back smoke.
  - **Re-baseline**: update the panel tests through the T515 helper and record them in `test-rebaseline.md`.
  - **Resources**: unity, emulator.
  - **Covers**: FR-020–FR-022, FR-024; research R11.
- [ ] T536 [US6] UI/UX Developer (Art Director and Test/QA lenses), after T535.
  - **Consistency pass on `PanelScaffold`**:
    - Market (inventory, priorities, expansion);
    - Contracts;
    - Items;
    - Research and Industry;
    - Prestige;
    - Settings;
    - `OfflineSummaryPanel` (the style pass only; T540 adds the offline-milestone listing);
    - `StartupStatusPanel`;
    - notices, errors and confirmations.
  - **Paths**: `Assets/IdleFactory/Unity/UI/Panels/`, `Assets/IdleFactory/Unity/UI/RunShellDocument.cs`, `Assets/IdleFactory/Tests/PlayMode/F006/SurfaceConsistencyTests.cs`.
  - **Fix #555**: wire `StartupStatusPanel.ApplyReducedMotion` in production.
  - **Tests**: `SurfaceConsistencyTests` (scaffold classes and templates on every panel) and the `ReducedMotionTests` extension for #555.
  - **Re-baseline**: record the affected panel tests.
  - **Resources**: unity.
  - **Covers**: FR-015, FR-023, FR-024, SC-009; research R11.

## Phase 8: User Story 1 — The factory is the screen (P1, phase P3)

**Goal**: A slim, labelled persistent layer. The six-tab bar is removed.

**Independent test**: `PersistentLayerTests` on every class, plus the probe measurements against the baseline (SC-001 and SC-010 guidance).

- [ ] T537 [US1] UI/UX Developer (Software Architect and Test/QA lenses), after T535 and T538.
  - **Persistent layer**: slim `Assets/IdleFactory/Unity/UI/FactoryRun.uxml`, `.uss` and `RunShellDocument.cs` per `contracts/ui-surfaces-f006.md` §1.
    - Remove the six-tab bar and the RP caption.
    - Show:
      - Cash and store fill;
      - the hub entry (icon plus visible text label from `world-copy.md`, readable at 200 %, 44 pt, accessible name, system-attention dot);
      - the production attention indicator;
      - manual mining;
      - Overdrive (compact, distinct ready state);
      - zoom only on pointer profiles or with `showZoomButtons`.
    - Phone portrait: a top strip plus a bottom row. Phone landscape: left and right columns.
  - **Shim**: retire the `ShellTab` shim from the UXML. It stays internal until no caller remains.
  - **Tests**: `PersistentLayerTests` (every class at 100, 150 and 200 %, including 640×360 at 200 %: contents, no clipping, ≥ 44 pt, label not truncated) and the zoom matrix (touch or pointer × preference × class).
  - **Re-baseline**: `RunHudLayoutTests`, `RunKeyboardFocusTests`, `RunTextScaleTests`, `NavigationInputTests`, `RunDeviceProfileTests`, `PixelCrispnessTests` and `DevToolsOverlayPlacementTests`.
  - **Measure**: run the T502 probe and record before→after in `reviews/baseline.md`.
  - **Resources**: unity, emulator.
  - **Covers**: FR-007–FR-010, FR-008a, SC-001, SC-010; research R11.
- [X] T538 [P] [US1] Gameplay Developer (Security/Privacy Low-tier and Test/QA lenses), after T500.
  - **Store**: `Assets/IdleFactory/Unity/Persistence/DisplayPreferencesStore.cs` v2 per R20:
    - `display-v2.json` = `{"version":2,"reducedMotion":…,"showZoomButtons":bool}`;
    - "If display-v2.json exists, read it with the v2 strict parser; invalid → defaults plus diagnostic, no v1 fallback";
    - "If absent, read display-v1.json with the unchanged v1 parser and set `showZoomButtons = false`";
    - writes go atomically through the temp file `display-v2.json.tmp`, and v1 stays in place;
    - `showZoomButtons` is rejected in v1;
    - 4096-byte cap.
  - **Settings sheet**: add a "Show zoom buttons" toggle.
  - **Tests**: `DisplayPreferencesV2Tests` (v1 read, migration, malformed or unknown v2, atomic write, temp name) and the `DisplayPreferencesStoreTests` re-baseline, plus a `[Category("StandalonePlayer")]` preferences round trip.
  - **Resources**: unity.
  - **Covers**: FR-003, FR-008a; research R20.

## Phase 9: User Story 5 — Building and progress feel rewarding (P2, phase P3)

**Goal**: A contextual build flow, development stages and milestones.

**Independent test**: `BuildCardTests`, `DevelopmentStageTests`, `StageTriptychDeltaTests` and `MilestoneWatcherTests`, plus the SC-006 ordering trial (T546).

- [ ] T539 [US5] UI/UX Developer (Test/QA lens), after T534.
  - **Card**: `Assets/IdleFactory/Unity/UI/Panels/BuildCard.cs`, `.uxml` and `.uss`, built from the `BuildSheetPresenter` data.
    - Docked like the machine card. The two are mutually exclusive.
    - Types are listed with icon, cost and affordability.
  - **Preview**: available locations are highlighted. The ghost preview (frame 0, 60 % alpha) comes through `SlotGridView`, with the text line "Selected: <type> at location N". The preview location stays 100 % visible.
  - **Confirm and Cancel**: side by side, each at least 44 pt.
    - Not enough Cash: Confirm is disabled and the card shows "Need $X more".
    - Success: build dust, then the foundation, then hand-over to the machine card.
  - **Tests**: `BuildCardTests` (enough Cash, not enough Cash, Back cancels, rotation, preview visibility) and the `BuildSheetPanelTests` re-baseline.
  - **Resources**: unity.
  - **Covers**: FR-032, FR-033, FR-034; research R13.
- [ ] T540 [US5] Gameplay Developer (Game Systems/Economy Low-tier, Art Director and Test/QA lenses), after T527, T528, T533 and T536.
  - **Stages**: `DevelopmentStage.cs`: "`t ≤ 0 → Early`; otherwise `clamp(floor(3·u/t), 0, 2)`", plus the activity and level density bins.
  - **Dressing**: stage-driven deck-lit area, edges, prop weights and depot variant in `FactoryDeckView` and `DepotLinkView`. They reset with prestige; prestige count is never an input.
  - **Milestones**: `MilestoneWatcher.cs` detects a factory-level increase and prestige completion.
    - `Assets/IdleFactory/Unity/UI/MilestoneBanner.cs`: non-blocking, at most 2.5 s, dismissible, copy from `world-copy.md`.
    - A deck reveal and a 2 s foundation highlight. Static under reduced motion.
    - Offline milestones appear in the summary only: T540 adds the listing to `OfflineSummaryPanel`, after T536.
  - **Tests**:
    - `DevelopmentStageTests` (4 and 8 of 12, `t ≤ 0`, no prestige input);
    - `StageTriptychDeltaTests` (at least 25 percentage points of deck-lit share between adjacent stages, plus a grayscale delta at n_min);
    - `MilestoneWatcherTests`;
    - `MilestoneBannerTests`.
  - **Resources**: unity.
  - **Covers**: FR-035, FR-036, SC-006; research R14, R15.

## Phase 10: Verification and evidence (phase P4)

- [ ] T541 [P] DevOps/Operations (Test/QA lens), after T537.
  - **Retarget**: in `scripts/android-emulator-journey.ps1`, move the `Default`, `F003`, `F004` and `F005` scenarios from bottom-bar fraction taps to a hub-entry tap helper.
  - **New scenario**: add `F006` to `$knownScenarios`: select, upgrade, hub → Contracts → back, build, rotation with the card open.
  - **Zoom evidence**: through the button or settings path. Pinch stays Play Mode only.
  - **Validation**: without an emulator (parsing, unknown step, lease conflict).
  - **Resources**: none.
  - **Covers**: SC-007, SC-009; research R19.
- [ ] T542 Test/QA, after T536–T541.
  - **Suites**: full Edit Mode and Play Mode with the test manifest, then `scripts/test-unity.ps1 -StandaloneWindowsPlayer` with every F006 `StandalonePlayer` case.
  - **Checks**: console, `scripts/test-art-pipeline.ps1` and `scripts/check-settings-noise.ps1 -Commit <sha>`.
  - **Emulator**: `scripts/android-emulator-journey.ps1` for all retargeted scenarios plus `-Scenario F006`.
  - **Manual input**: the real-input checklist on the Windows player (hub, card, build card, Back) via user32 input.
  - **Fixtures**: save fixtures before F006 load and re-save byte-identically; the balance trace is unchanged.
  - **Guidance re-run**: re-run `InteractionCountJourney` and the `ShellGeometryProbe` (SC-001, SC-002, SC-003, SC-010) on the final build. Record before→after in `reviews/baseline.md`.
  - **Record**: `reviews/final-test-evidence.md` and the completed `test-rebaseline.md`.
  - **Resources**: unity, emulator.
  - **Covers**: SC-007, SC-009, SC-011.
- [ ] T543 Play-Tester (Game Designer/PO lens) together with an independent UI/UX Reviewer. They run after each P3 batch (A, B, C as defined in T500) and finally after T542.
  - **Play-Tester**: strategy and player sessions on the merged build (FR-045). Findings for the owner go to `reviews/playtest-agent-f006-<batch>.md`.
  - **UI/UX Reviewer**: reviews the batch's surfaces on emulator and Windows captures, including the U-10 interaction checkpoint. Findings go to `reviews/uiux-f006-<batch>.md`.
  - **Gate**: the next batch starts only after the owner has seen the report. Records go in `specs/006-world-first-factory-experience/reviews/`.
  - **Resources**: unity, emulator.
  - **Covers**: FR-045.
- [ ] T544 Performance Engineer, after T542.
  - **Measure B4b** on W006-V with the card open: all frozen budgets and the 600-frame player allocation run.
  - **Record**: final `reviews/performance-b4.md`, with the physical-device gap stated.
  - **Resources**: unity, emulator, gpu.
  - **Covers**: FR-006, SC-008; research R16.
- [ ] T545 [P] Test/QA (Asset Artist lens), after T537 and T540.
  - **Seeds**: seed lists and pass/fail oracle scripts under `tools/art-pipeline/trials/f006/` for the SC-005 final sheets (with P3 cues) and for:
    - SC-004: 10 seeded mixed-state factory captures from the shipped build;
    - SC-006: 10 shuffled stage triptychs with varied layouts and mixes.
  - **Captures**: from real builds at the emulator and Windows classes.
  - **Validation**: pytest for the oracles.
  - **Resources**: unity, emulator.
  - **Covers**: SC-004, SC-006; research R18.
- [ ] T546 *Parent* after T545: Run the trials.
  - **SC-004**: the owner timed trial (3.0 s and 5.0 s guidance).
  - **SC-006**: independent agent ordering.
  - **SC-005 final** (binding): repeat the blind naming in grayscale and three CVD filters, and the owner's timed cue trial, on the shipped build. Include the P3 cues: upgrade marker, production-attention markers, ghost preview, hub dot, and the card next to its machine.
  - **Record**: in `reviews/readability-f006.md`, with the guidance results stated against the baseline.
  - **Covers**: SC-004, SC-006.
- [ ] T547 World Designer (read-only conformance), after T540 and T537.
  - **Check**: the final World Bible checklist covering deck, depot, links, props, vein, stage dressing and every world-facing string in the shipped build.
  - **Record**: `reviews/world-bible-conformance.md` (final): zero conflicts and no unowned string.
  - **Resources**: none.
  - **Covers**: FR-047, SC-013.
- [ ] T548 Independent read-only final reviews, after T542–T547:
  - Game Designer/PO;
  - Software Architect;
  - Test/QA;
  - UI/UX Reviewer;
  - Art Director;
  - Technical Artist;
  - World Designer;
  - Performance;
  - Security/Privacy (preferences v2);
  - Game Systems (Low).

  Record them in `specs/006-world-first-factory-experience/reviews/final-acceptance.md`.
  - **Resources**: none.
  - **Covers**: FR-001–FR-047.
- [ ] T549 *Parent* after T548: The owner gives visual acceptance per surface and screen class.
  - **Evidence**: before/after sheets from real builds (emulator and Windows) for every key state:
    - no panel;
    - card;
    - details;
    - building;
    - hub;
    - system view;
    - offline summary;
    - reduced motion;
    - 200 % text;
    - the stage triptych.
  - **Also record**: the guidance measurements versus the baseline and the A-9 limitation, in `reviews/final-acceptance.md`.
  - **Covers**: FR-042, SC-012.

## Phase 11: Closeout hooks and convergence (parent)

- [ ] T550 *Parent* after T549:
  1. `scripts/maqa-sync-task-checkboxes.ps1 -TasksFile specs/006-world-first-factory-experience/tasks.md`. Over all four steps T550 writes `specs/006-world-first-factory-experience/tasks.md`, `specs/006-world-first-factory-experience/learn.md` and `wiki/`.
  2. `$speckit-wiki-ingest specs/006-world-first-factory-experience`
  3. `$speckit-learn-review`
  4. `$speckit-wiki-lint`
  - **Covers**: constitution X.
- [ ] T551 *Parent* after T550:
  1. `scripts/check-after-implement-hooks.ps1 -FeatureDir specs/006-world-first-factory-experience`
  2. `$speckit-converge` until no work remains
  3. `scripts/maqa-stale-worktrees.ps1`, with the removal commands handed to the user.

---

## Dependencies and execution order

### Gates (strict)

- T500 (gates; OD-11 confirmed) → everything, including T514.
- T504 (OD-9a, World Bible approved) → T506.
- T509 (OD-9b direction) → T516, T517 and all P2/P3 production.
- T518 (OD-10, OD-12) → T519–T524.

### P0 chain

- T502, T503 and T505 can start in parallel after T500.
- T503 → T504.
- T504 → T506.
- T505 and T506 → T507.
- T507 → T508 → T509.

### P1 chain

- T510 and T505 → T512.
- T511 → T515.
- T513 can start after T500.
- T514 can start after T500.

### P2 chain

- T516 and T517 → T518.
- T518 → T519, T520, T521, T522, T523 and T524, all in parallel.
- T519–T524 → T525.
- T525 and T513 → T526.
- T514 and T512 → T532 (the model; runs in P2).
- T526 and T512 → T527.
- T527 and T532 → T528.
- T526 and T532 → T529.
- T527–T529 → T530 and T531.

### P3 chain

- P3 runs in batches A (T532–T534), B (T535–T538) and C (T539–T540). After each batch, T543 runs before the next batch starts.
- T533 needs T511.
- T534 needs T512, T532 and T533.
- T535 needs T533 and T534.
- T536 needs T535.
- T538 can start after T500.
- T537 needs T535 and T538.
- T539 needs T534.
- T540 needs T527, T528, T533 and T536.

### P4 chain

- T541 needs T537.
- T542 needs T536–T541.
- T543 runs once per P3 batch and finally after T542.
- T544 needs T542.
- T545 needs T537 and T540, then T546.
- T547 needs T537 and T540.
- T548 needs T542–T547, then T549.
- T549 → T550 → T551.

### Story completion order

1. US8 (gate)
2. US7 (seam)
3. US2 (world)
4. US4 (state)
5. US3 (card)
6. US6 (hub)
7. US1 (screen)
8. US5 (build and progress)

US1 depends on US6 because the tab bar is removed only after the hub exists.

## Parallel Execution

### Parallel opportunities

Work-unit ordering in the ledger controls orchestration; `[P]` markers are local hints only. A parallel start needs verified prerequisites for every involved unit, disjoint write paths and the leases each unit needs.

- **P0**: T502 (Unity), T503 (document) and T505 (tooling, Edit Mode export only).
- **P1**: T510, T511 and T513 run in different files. The Unity lease serializes their test runs. T514 runs after T509. T538 can run any time after T500.
- **P2**: the families T519–T524 run in parallel. The gpu lease serializes the Blender renders in T519–T521; T522–T524 are scripted and need no gpu.
- **P3**: T539 and T540 can run in parallel with T535 and T536 after their dependencies are met, on disjoint paths.

### Example: P2 family wave after T518

- **Blender, serialized by gpu**: T519 (ground and foundation), T520 (depot and deposit), T521 (props and edges).
- **Scripted, at the same time**: T522 (link and FX), T523 (UI), T524 (A-03 anchor).

## Subagent Handoff Examples

**Test-oriented unit: WU15/T514 (`CompletedCycles`)**

- **Assignment**: a Gameplay Developer with the Software Architect and Game Systems/Economy lenses, Medium tier at `high` reasoning. Start only after OD-11 is confirmed in T500.
- **Context to give**:
  - the T514 text;
  - `contracts/presentation-model.md` §1;
  - research R08;
  - the data-model ownership rows;
  - spec FR-001, FR-002 and FR-029;
  - `wiki/pages/f006-world-presentation-design.md`.
- **Test-first**: it writes `CompletedCyclesLifecycleTests`, `CompletedCyclesPropertyTests` and `CompletedCyclesNoObserverEffectTests` first, red against the unchanged Core.
- **Allowed changes**: only `RunSimulator.cs`, `RunSession.cs`, `MachineStateView.cs` and the API fixture.
- **Report**: the save and trace SHA comparison, and the test counts.

**Implementation unit: WU35/T534 (machine card)**

- **Assignment**: a UI/UX Developer with the Software Architect and Test/QA lenses, Medium tier. Start only after the parent has acquired `unity,emulator`.
- **Context to give**:
  - the T534 text;
  - `contracts/ui-surfaces-f006.md` §2 and §4;
  - research R10;
  - spec FR-016 to FR-019, with the clarified 30/45 % rule;
  - `wiki/pages/run-panel-conventions.md` and `ui-toolkit-hud-layout-pitfalls.md`.
- **Allowed changes**: only `Assets/IdleFactory/Unity/UI/MachineCard.*`, `Panels/MachinePanelPresenter.cs` and its tests, and the affected test re-baseline rows.
- **Camera**: changes go only through `OpenSheet` and `CloseSheet`.
- **Report**: the screen-class matrix results at 100, 150 and 200 %, and the re-baselined files.

## Implementation Strategy

### Incremental delivery

1. **P0.** No code ships. The owner approves the World Bible and the direction.
2. **P1.** Refactors that the player cannot see. Each merge keeps every suite green.
3. **P2.** The new world look ships on the old interface. That merge is a visible milestone with its own B4a and SC-005.
4. **P3.** The interface rebuild lands in story order: state, card, hub, screen, then build and progress. Each batch gets a Play-Tester report and a UI/UX review.
5. **P4.** Evidence and owner acceptance.

**MVP**: the minimum viable increment is P0 + P1 + P2 + US4 + US3. At that point the factory reads as a place and machines are operated in place. Owner acceptance still requires all stories.

### Context-orchestrated subagent strategy

- The parent sends a compact packet per Issue: task text, contract sections, wiki pages, paths, commands, risks.
- Medium tier by default. Seam and Core work (T510, T511, T512, T514) runs at high reasoning.
- Reviews run in fresh sessions. QA fixes go back to the same worker.

## Requirement coverage

| Requirement | Tasks |
|---|---|
| FR-001, FR-002 | T514, T532, T542 |
| FR-003 | T538 |
| FR-004 | T516, T518, T526 |
| FR-005 | T533, T534, T537 |
| FR-006 | T529, T531, T544 |
| FR-007–FR-010, FR-008a | T537, T538 |
| FR-010a | T520, T528 |
| FR-011–FR-012 | T519, T527 |
| FR-013 | T520, T522, T528 |
| FR-014 | T513, T517, T524, T527 |
| FR-015 | T521, T522, T527, T528, T536 |
| FR-016–FR-019 | T534 |
| FR-020–FR-022 | T535, T537 |
| FR-023 | T533, T536 |
| FR-024 | T511, T534, T535, T536 |
| FR-025–FR-028 | T524, T532 |
| FR-029 (completion signal, end to end) | T514 (count), T532 (delta), T529 (pool wiring), T522 (art) |
| FR-030 (no numbers in the world) | T532 |
| FR-031 (shape and CVD, no motion) | T522, T530, T546 |
| FR-032, FR-033 | T539 |
| FR-034 | T529, T534 (upgrade glint, switch-on), T539 (build dust) |
| FR-035, FR-036 | T540, T521 |
| FR-037, FR-038 | T510, T512, T527 |
| FR-039 | spec future table, `wiki/pages/future-free-building-vision.md` (done in plan) |
| FR-040, FR-041 | T502, T505, T506, T507, T508, T509 |
| FR-042, FR-043 | T530, T546, T549 |
| FR-044 | T506, T516, T519–T526 |
| FR-045 | T500, T508, T543 (per batch: Play-Tester and UI/UX), T548 |
| FR-046 | T502, T537 |
| FR-047 | T503, T504, T525, T547 |
| SC-001, SC-002, SC-003, SC-010 (guidance) | T502, T534, T537, T542 |
| SC-004, SC-006 (guidance) | T545, T546 |
| SC-005 | T530 (after P2), T546 (final, including P3 cues) |
| SC-007 | T514, T515, T542 |
| SC-008 | T531, T544 |
| SC-009 | T533, T536, T537, T542 |
| SC-011 | T512, T542 |
| SC-012 | T509, T549 |
| SC-013 | T525, T547 |
