---
description: "Task list for Feature 005: Economy and Contracts"
---

# Tasks: Economy and Contracts

**Input**: Design documents in `specs/005-economy-contracts/`: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md) (F5-R01–F5-R18), [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md) and [reviews/plan-reviews.md](reviews/plan-reviews.md).

**Tests**: Required. Constitution IV and AGENTS.md require Edit Mode tests for gameplay and economy rules, Play Mode tests for UI and runtime integration, and `[Category("StandalonePlayer")]` tests for player-relevant acceptance.

**Organization**:
- Phases 1–2 (setup, foundation) follow owner decision D-B: #353 measurement, #356 refactor, follow-ups, tooling and strategy framework, content v3.
- User-story phases follow in priority order.
- Every task names its primary role, its secondary lenses, its dependencies (`after Txxx`) and the plan step it implements.
- The context-orchestration work-unit ledger below is the execution control plane for the parent agent and its clean-context subagents.
- Task IDs start at T300. F003 ends at T251. The F004 draft (T193–T242) is renumbered before its own sync.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: disjoint paths and no incomplete dependency, so safe to author in parallel. Verification still needs the single Unity slot.
- **[U]**: needs the `unity` lease (`unity,emulator` when the worktree's `Library/` may target Android). **[E]**: needs the emulator. See [quickstart.md](quickstart.md) › Leases per step.
- Paths are repository-relative. The Core rules live in `Assets/IdleFactory/Core/Run/`.

---

## Context Orchestration Process

### Parent Orchestrator Rules

- Work units are executed in ledger order, unless the ledger marks units as parallel candidates and their prerequisites are verified. Task-level `[P]` markers are local hints only.
- Task checkboxes are the implementation checklist. A unit is complete only when every referenced task is checked after parent verification. GitHub Issues remain the MAQA workflow-status authority, and the checkboxes are mirrored from them (`scripts/maqa-sync-task-checkboxes.ps1`).
- Each delegated unit gets a clean subagent session with one authoritative MAQA Issue and worktree. The prompt carries the full handoff packet and does not rely on prior conversation state.
- Each worker stays inside its listed write scope and stops before editing an unassigned path or a shared serialized Unity asset (scenes only through Unity tooling).
- Test tasks follow fail-first behaviour where practical. A red-only unit is never accepted.
- Before spawning a worker, the parent acquires the leases the unit needs (`scripts/resource-lease.ps1`, owner `issue-<n>`; see quickstart.md › Leases per step). It releases them after QA and merge.
- The model tier follows AGENTS.md routing: Medium by default, `high` reasoning where a task says so. Escalation needs a recorded reason.
- Merges into the shared files `docs/contracts/run-reason-codes.md` and `Assets/IdleFactory/Unity/Content/RunContentValidator.cs` happen one after another, and each later unit rebases first.
- Before a dependent unit starts, the parent inspects diffs, runs targeted verification, checks for unrelated changes, and fixes or re-delegates inconsistencies.
- The parent owns final validation and reruns the completion gates. Workers and QA never run the `after_implement` hooks.

### Required Subagent Handoff Packet

Every assignment includes:

- the work unit ID, phase, task ID, MAQA Issue, exact task text and exact paths;
- the role, review lenses, complexity tier and model/reasoning level;
- the relevant `spec.md` FR, SC and edge cases, `research.md` F5-R ids, `data-model.md` rows, `contracts/` sections, and the owner decisions D-A to D-P;
- the non-negotiable constraints:
  - Core stays engine-free;
  - checked integers stay ≤ 10^18, with no `double` in authoritative rules;
  - no reflection deserialization;
  - nothing is published partially;
  - deterministic, partition-invariant simulation;
  - the session log never alters simulation, save or timing;
  - DevTools never reach release builds;
  - `runInBackground` stays 0;
- the accepted prerequisite commits and the current implementation status;
- allowed and forbidden paths and tools;
- Unity `6000.6.0f1`, the target platforms, and the required Edit Mode, Play Mode or player validation, with exact commands;
- the relevant wiki pages (`wiki/pages/f005-plan-overview.md`, `f005-timeline-mapping-and-auto-delivery.md`, `f005-session-log-and-devtools.md`, `run-balance-policy-and-f005-matrix.md`, `run-contracts.md`, `run-persistence.md`, `save-v2-document.md`, `player-level-test-gaps.md`, as applicable);
- the required response: files changed, task IDs completed, tests run with results, expected failures, assumptions, unresolved risks, parent follow-up, and a done or blocked verdict.

### Parent Verification Gate

For each unit, the parent:

1. inspects `git diff -- <unit paths>` plus every extra path the worker reported, and confirms that edits stay in scope or are justified adjacent changes;
2. runs the targeted `scripts/test-unity.ps1 -TestFile <file>` commands and the suite named in the unit;
3. rereads the contract and data-model sections for any public boundary change, and reviews the public API baseline diff;
4. confirms that `Library/`, the `ProjectSettings/` noise (`scripts/check-settings-noise.ps1 -Commit <sha>`), legacy Core, the Save V1 fixtures and unassigned serialized assets are unchanged unless the unit names them;
5. requires independent QA PASS on the exact commit before marking the unit.

### Context Compaction Recovery

After compaction:

1. Read this process, the ledger and the checklist.
2. Run `git status --short` and `scripts/resource-lease.ps1 -Status`, and inspect existing diffs.
3. Resume at an earlier partially changed unit if one exists. Otherwise resume at the first unit with unchecked tasks whose prerequisites are accepted.
4. Reconstruct state from Issue status, merged commits, mirrored checkboxes, diffs and targeted tests.
5. Finish and verify that unit before opening another worker.

### Work Unit Ledger

Rows are listed in execution order with one task per unit. Unit numbers are not derived from task numbers: T357–T360 were added after task generation. T360 is WU18; the later units were renumbered by one on 2026-10-10. T361–T363 (owner decisions 2026-10-15 on the batch-A Play-Tester report) are WU48–WU50; the later units were renumbered by three on 2026-10-15. T364 (owner decision 2026-10-21, #403) is WU51; the later units were renumbered by one on 2026-10-21. T365 and T366 (owner decisions 2026-10-22 on the batch-B Play-Tester report, #408) are WU54 and WU55; the later units were renumbered by two on 2026-10-22. T367 (owner decision 2026-10-27 on the T352 review, #417) is WU64; the later units were renumbered by one on 2026-10-27. Issue bodies of T343–T356 therefore still name their old unit numbers. Every prerequisite names an earlier unit.

| Unit | Phase | Tasks | Atomic scope and touched paths | Prerequisites | Required handoff sources | Parent verification |
|------|-------|-------|--------------------------------|---------------|--------------------------|---------------------|
| WU01 | Phase 1: Setup and design gates | T300 | W003-B Option 0 measurement on `a41c53f` (separate worktree); record `reviews/w003b-measurement.md` | None | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | record reviewed by the Architect; host conditions and spread recorded |
| WU02 | Phase 1: Setup and design gates | T301 | Policy r2 rules document `reviews/balance-policy-r2.md` (no code) | None | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | Game Systems and Product review of the document; no code diff |
| WU03 | Phase 2: Foundational (blocks every story) | T302 | API baseline then `RunPersistence` partial split (`Assets/IdleFactory/Core/Run/Application/`) | None | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | `PublicApiSnapshotTests` unchanged; full Edit Mode and Play Mode; Windows player suite |
| WU04 | Phase 2: Foundational (blocks every story) | T303 | `RunSnapshots` split (`Assets/IdleFactory/Core/Run/Application/`) | WU03 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | snapshot unchanged; full Edit Mode and Play Mode |
| WU05 | Phase 2: Foundational (blocks every story) | T304 | Internal command mutators, `InternalsVisibleTo` (Core `AssemblyInfo`) | WU04 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | visibility-only snapshot diff; Edit Mode, Play Mode, Windows player suite |
| WU06 | Phase 2: Foundational (blocks every story) | T305 | #355 reason table `docs/contracts/run-reason-codes.md`, `ReasonCodeContractTests` | WU05 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU07 | Phase 2: Foundational (blocks every story) | T306 | #354 checkpoint interval, bypasses, failing-write and order tests (`RunPersistence`, `RunDesktopLifecycleTests`) | WU05 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU08 | Phase 2: Foundational (blocks every story) | T307 | `IRunEventSink`, `RunEvents` (Core Application) | WU05 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU09 | Phase 2: Foundational (blocks every story) | T308 | DevTools asmdef, time-scale seam and clamp, dev save root, build variants (`Assets/IdleFactory/DevTools/`, `FactoryRuntimeHost.cs`, `PlatformBuildCommands.cs`) | WU05 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU10 | Phase 2: Foundational (blocks every story) | T309 | `scripts/check-release-build.ps1` (Windows and Android scan) | WU09 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | script run against an existing build; exit codes checked |
| WU11 | Phase 2: Foundational (blocks every story) | T310 | `scripts/test-unity.ps1 -Category` | WU05 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | one `-Suite EditMode -Category` run; rejection with `-StandaloneWindowsPlayer` |
| WU12 | Phase 2: Foundational (blocks every story) | T311 | Strategy framework and first strategies (`Assets/IdleFactory/Tests/EditMode/Balance/`) | WU05 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU13 | Phase 2: Foundational (blocks every story) | T312 | v3 definition fields, `TestCatalogBuilder` (`Assets/IdleFactory/Core/Run/Content/`) | WU05 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU14 | Phase 2: Foundational (blocks every story) | T313 | Content reader v3, transitional reward adapter, shipped content migration, schema, guide | WU13 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | full Edit Mode and Play Mode; parity tests; transitional golden labelled |
| WU15 | Phase 2: Foundational (blocks every story) | T314 | Structural content rules (`RunContentValidator.cs`) | WU14 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU16 | Phase 2: Foundational (blocks every story) | T315 | Parent-only gate: full Play Mode and Windows player runs after content v3 | WU14, WU15, WU06, WU07, WU08, WU09, WU10, WU11, WU12 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | parent runs the full Play Mode suite and `scripts/test-unity.ps1 -StandaloneWindowsPlayer`; console clean |
| WU17 | Phase 2: Foundational (blocks every story) | T357 | Play-Tester batch-1 report `reviews/playtest-agent-batch-1.md` (read-only) | WU16 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | owner reviews the report; no code diff |
| WU18 | Phase 2: Foundational (blocks every story) | T360 | Option 1 index-based hot loop in the `RunSimulator` completion pass; PF-3 re-measure appended to `reviews/w003b-measurement.md` | WU16, WU01 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md, reviews/w003b-measurement.md | bit-identical W003 hashes; PF-1 pass numbers unchanged; full Edit Mode, Play Mode and Windows player suite; quiet-host re-measure recorded; Architect and Performance review |
| WU19 | Phase 3: User Story 1 — Sell stock by hand (P1) 🎯 MVP | T316 | `SalePricing` (Domain), auto-sell and valuation routing, `chain.dominance` | WU16, WU01, WU18 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU20 | Phase 3: User Story 1 — Sell stock by hand (P1) 🎯 MVP | T317 | `RunCommands.Sell.cs`, economy snapshot, sale events | WU19 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU21 | Phase 3: User Story 1 — Sell stock by hand (P1) 🎯 MVP | T318 | W003-B re-measure, append to `reviews/w003b-measurement.md` | WU20 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | measurement appended; 10 % regression guard evaluated |
| WU22 | Phase 3: User Story 1 — Sell stock by hand (P1) 🎯 MVP | T319 | Market sell UI (`Assets/IdleFactory/Unity/UI/`), `MarketSellPanelTests` | WU20 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU23 | Phase 5: User Story 4 — Prices grow with the factory (P1) | T323 | `BuildPricing.cs`, `Build(expectedPrice)`, snapshot cache | WU16 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU24 | Phase 5: User Story 4 — Prices grow with the factory (P1) | T324 | Build sheet price UI, `BuildPricePanelTests` | WU23 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU25 | Phase 3b: User Story 10 — Mine ore by hand (P1) | T358 | `RunCommands.Mining.cs`, rate counter and remainder, `manualYield`, mining rules | WU20, WU23 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU26 | Phase 3b: User Story 10 — Mine ore by hand (P1) | T359 | Ore vein overlay UI, `OreVeinPanelTests` | WU25 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU27 | Phase 4: User Story 2 — Contracts are a choice, not a substitute (P1) | T320 | `ContractRewards.cs` (Domain), ratio rewards, ladder, adapter removal, contract rules | WU20 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU28 | Phase 4: User Story 2 — Contracts are a choice, not a substitute (P1) | T321 | `ContractBoard` cooldowns, bonus state, version, prestige reset, accept checks | WU27 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU29 | Phase 4: User Story 2 — Contracts are a choice, not a substitute (P1) | T322 | Production-objective state and command paths | WU27 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU30 | Phase 6: User Story 3 — Auto-deliver a contract (P2) | T325 | `TimelineMapping.cs` and `Advance` signature (bit-identical) | WU28, WU29, WU01 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | bit-identical W003 hashes; live and offline stamp assertions |
| WU31 | Phase 6: User Story 3 — Auto-deliver a contract (P2) | T326 | Simulator scratch and commit, auto-delivery, reserve, `SetAutoDeliver` | WU30, WU18 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU32 | Phase 6: User Story 3 — Auto-deliver a contract (P2) | T327 | Production counting, bonus trigger, lazy availability, outcomes and events | WU31 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU33 | Phase 6: User Story 3 — Auto-deliver a contract (P2) | T328 | `AutoDeliverEquivalenceTests` cases (l)–(s) | WU32 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU34 | Phase 6: User Story 3 — Auto-deliver a contract (P2) | T329 | W003-B re-measure after T325–T327 | WU30, WU31, WU32 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | measurement appended; passes per timestamp recorded |
| WU35 | Phase 6: User Story 3 — Auto-deliver a contract (P2) | T330 | Contracts panel and tab badge UI, `ContractsAutoDeliverPanelTests` | WU32 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU36 | Phase 7: User Story 5 and User Story 6 — Next goal and stronger runs (P2) | T331 | `PrestigeThreshold.cs`, Industry diminishing tables, threshold and ceiling rules | WU16 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU37 | Phase 7: User Story 5 and User Story 6 — Next goal and stronger runs (P2) | T332 | `RunSnapshots.Goals.cs` | WU36 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU38 | Phase 7: User Story 5 and User Story 6 — Next goal and stronger runs (P2) | T333 | Goal line and prestige threshold UI, `GoalsLinePanelTests` | WU37, WU36 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU39 | Phase 8: Save V3 (persistence for US2, US3 and US6) | T334 | Version-parametrized save mapper, V3 writer, restore setters (`Assets/IdleFactory/Unity/Persistence/`) | WU32 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU40 | Phase 8: Save V3 (persistence for US2, US3 and US6) | T335 | Content drift, fail-closed matrix, V2 regression, fault injection, forward compatibility | WU39 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU41 | Phase 8: Save V3 (persistence for US2, US3 and US6) | T336 | `W005-L` fixture, parity test, early timing, `reviews/w005l-measurement.md` | WU40, WU36 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | fixture hash and parity test; measurement record |
| WU42 | Phase 9: User Story 9 — Waking from sleep is an offline return (P2) | T337 | Live gap: host UTC read, `RecordSuspension(atUtcMs)`, `LiveGapTests` | WU07, WU09, WU30, WU39 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU43 | Phase 10: User Story 8 — Local session log (P3) | T338 | `SessionLogWriter.cs`, `SessionLogExport.cs`, schema and isolation tests | WU08, WU40 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU44 | Phase 10: User Story 8 — Local session log (P3) | T339 | Android backup rules, manifest check (`Assets/Plugins/Android/`, `check-release-build.ps1`) | WU43, WU10 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | GLES3 emulator build runs; manifest check passes; `git diff ProjectSettings/` reviewed |
| WU45 | Phase 10: User Story 8 — Local session log (P3) | T340 | Session-log Market section and DevTools overlay polish, `SessionLogPanelTests` | WU43, WU09 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU46 | Phase 11: User Story 7 — Strategy trace, balance r3 and time acceleration (P2) | T341 | Policy r2, remaining strategies, ensemble, FR-024 gates (`Assets/IdleFactory/Tests/EditMode/Balance/`) | WU02, WU12, WU20, WU32, WU23, WU36, WU25, WU11 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU47 | Phase 11: User Story 7 — Strategy trace, balance r3 and time acceleration (P2) | T342 | Play-Tester batch-A report (read-only) | WU22, WU26, WU29, WU24 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | owner reviews the report |
| WU48 | Phase 11: User Story 7 — Strategy trace, balance r3 and time acceleration (P2) | T361 | Batch-A defects F7 and F6: DevTools time overlay renders and operates; real-input Mine hold test (`Assets/IdleFactory/DevTools/`, `Assets/IdleFactory/Tests/DevTools/`, mining UI under `Assets/IdleFactory/Unity/UI/`, `Assets/IdleFactory/Tests/PlayMode/`) | WU47, WU09, WU26 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md, reviews/playtest-agent-batch-a.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; DevTools test assembly run; Windows development build shows the overlay; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU49 | Phase 11: User Story 7 — Strategy trace, balance r3 and time acceleration (P2) | T362 | Batch-A F8/F9 and owner decisions #382/#417: shell focus guard, `FocusWhenReady` abort, Store/Overdrive/Cash labels, locked Sell rows and shortfall text (`Assets/IdleFactory/Unity/UI/`, `Assets/IdleFactory/Tests/PlayMode/`) | WU47, WU22, WU38 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md, reviews/playtest-agent-batch-a.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; console clean; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS with the UI/UX lens |
| WU50 | Phase 11: User Story 7 — Strategy trace, balance r3 and time acceleration (P2) | T363 | Minimal content slice in revision 2: one cooldown offer, one bonus offer, one production objective (shipped content copies; transitional golden) | WU47, WU28, WU29 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md, reviews/playtest-agent-batch-a.md | content validator passes; full Edit Mode and Play Mode; transitional golden regenerated or relabelled per AR2-2; Game Designer review; independent QA PASS |
| WU51 | Phase 11: User Story 7 — Strategy trace, balance r3 and time acceleration (P2) | T364 | Session-log raise sites in Core (Prestige, Purchase, Bottleneck, Accept/Deliver/Abandon), Clock liveGap/forwardJump, immediate flush, and the T338 review items (`Assets/IdleFactory/Unity/Persistence/`, `Assets/IdleFactory/Core/`, `Assets/IdleFactory/Tests/`) | WU42, WU43 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test files>`; US8 Independent Test end-to-end; saves byte-identical with logging on and off; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS with the Security and Performance lenses |
| WU52 | Phase 11: User Story 7 — Strategy trace, balance r3 and time acceleration (P2) | T343 | Play-Tester batch-B report (read-only) | WU35, WU38, WU42, WU48, WU49, WU50, WU51 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | owner reviews the report |
| WU53 | Phase 11: User Story 7 — Strategy trace, balance r3 and time acceleration (P2) | T344 | Catalog-derived expectations audit (`Assets/IdleFactory/Tests/PlayMode/`) | WU22, WU24, WU35, WU38, WU45, WU26 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU54 | Phase 11: User Story 7 — Strategy trace, balance r3 and time acceleration (P2) | T365 | Batch-B defect F-B3: stale or wrong machine-panel guidance (blocked-reason text and fix buttons for a running machine) (`Assets/IdleFactory/Unity/UI/`, `Assets/IdleFactory/Tests/PlayMode/`) | WU49, WU53 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md, reviews/playtest-agent-batch-b.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; console clean; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS with the UI/UX lens |
| WU55 | Phase 11: User Story 7 — Strategy trace, balance r3 and time acceleration (P2) | T366 | Batch-B DevTools defects F-B2 and F-B4: overlay consumes pointer events; session-log Header only on a real time-scale factor change (`Assets/IdleFactory/DevTools/`, `Assets/IdleFactory/Tests/DevTools/`, session-log writer) | WU48, WU51, WU53 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md, reviews/playtest-agent-batch-b.md | diff inspection; DevTools test assembly run; `scripts/test-unity.ps1 -TestFile <named test file>`; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS |
| WU56 | Phase 11: User Story 7 — Strategy trace, balance r3 and time acceleration (P2) | T345 | Content revision 3 tuning, `reviews/balance-trace-r3.md`, `W005-L` regeneration | WU46, WU53 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | full Play Mode, Windows player and `-Category BalanceFull`; owner-facing record |
| WU57 | Phase 11: User Story 7 — Strategy trace, balance r3 and time acceleration (P2) | T346 | Play-Tester batch-C report (read-only) | WU56 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | owner reviews the report |
| WU58 | Phase 12: Player and device evidence | T347 | Full suites and console check, `reviews/full-suite-evidence.md` | WU56 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | parent rechecks the suite results |
| WU59 | Phase 12: Player and device evidence | T348 | StandalonePlayer journey, round trip, parity, log-failure tests | WU56 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | `scripts/test-unity.ps1 -StandaloneWindowsPlayer` |
| WU60 | Phase 12: Player and device evidence | T349 | `W005-L` timings, frame time, development-player acceleration run | WU56, WU59 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | quiet-host evidence; Performance verdict |
| WU61 | Phase 12: Player and device evidence | T350 | Release build scan on Windows and Android, `reviews/release-scan.md` | WU44, WU56 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | scan exit code 0 on both builds |
| WU62 | Phase 12: Player and device evidence | T351 | Emulator journey F005 steps (`scripts/android-emulator-journey.ps1`) | WU56, WU44 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | journey exits 0; emulator-only label |
| WU63 | Phase 13: Independent reviews, human gates and final acceptance | T352 | Independent UI/UX review `reviews/ui-ux-review.md` (read-only) | WU45, WU53, WU54, WU55 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | review record; findings dispositioned |
| WU64 | Phase 13: Independent reviews, human gates and final acceptance | T367 | T352 review fixes (owner decision 2026-10-27, #417): Industry pointer, Offers order and state, titles and copy, machine-panel focus, Build-a-Miner hint, Research card, USS rule, currency wording, QA-12 touch/pointer cases (`Assets/IdleFactory/Unity/UI/`, `Assets/IdleFactory/Tests/PlayMode/`, `contracts/ui-surfaces-f005.md`) | WU63 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md, reviews/ui-ux-review.md | diff inspection; `scripts/test-unity.ps1 -TestFile <named test file>`; full Play Mode; console clean; `scripts/check-settings-noise.ps1 -Commit <sha>`; independent QA PASS; independent UI/UX re-review addendum in `reviews/ui-ux-review.md` |
| WU65 | Phase 13: Independent reviews, human gates and final acceptance | T353 | Owner human playtest record (human gate) | WU56, WU57, WU64 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | owner record exists |
| WU66 | Phase 13: Independent reviews, human gates and final acceptance | T354 | Owner content approval `reviews/content-approval-r3.md` (human gate) | WU65 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | owner decision recorded |
| WU67 | Phase 13: Independent reviews, human gates and final acceptance | T355 | Parent-only final reviews and acceptance records | WU58, WU59, WU60, WU61, WU62, WU63, WU64, WU65, WU66 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | all verdicts recorded on the accepted commit |
| WU68 | Phase 13: Independent reviews, human gates and final acceptance | T356 | Parent-only closeout hooks, gate, convergence, stale-worktree report | WU67 | spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md, reviews/plan-reviews.md | `scripts/check-after-implement-hooks.ps1 -FeatureDir specs/005-economy-contracts` PASS; converge reports no work |

## Issue mapping

The owner-approved `$speckit-maqa-sync` created these Issues on 2026-10-10 (#362–#421). The resource labels below mark the leases each Issue needs. GitHub Issues stay the status authority.

| Task | Issue | Executed by | Resources |
|---|---|---|---|
| T300 | #362 | MAQA Issue | resource:unity, resource:emulator |
| T301 | #363 | MAQA Issue | none |
| T302 | #364 | MAQA Issue | resource:unity |
| T303 | #365 | MAQA Issue | resource:unity |
| T304 | #366 | MAQA Issue | resource:unity |
| T305 | #367 | MAQA Issue | resource:unity |
| T306 | #368 | MAQA Issue | resource:unity |
| T307 | #369 | MAQA Issue | resource:unity |
| T308 | #370 | MAQA Issue | resource:unity |
| T309 | #371 | MAQA Issue | none |
| T310 | #372 | MAQA Issue | resource:unity |
| T311 | #373 | MAQA Issue | resource:unity |
| T312 | #374 | MAQA Issue | resource:unity |
| T313 | #375 | MAQA Issue | resource:unity |
| T314 | #376 | MAQA Issue | resource:unity |
| T315 | #377 | parent | resource:unity |
| T357 | #378 | MAQA Issue | none |
| T316 | #379 | MAQA Issue | resource:unity |
| T317 | #380 | MAQA Issue | resource:unity |
| T318 | #381 | MAQA Issue | resource:unity |
| T319 | #382 | MAQA Issue | resource:unity |
| T323 | #383 | MAQA Issue | resource:unity |
| T324 | #384 | MAQA Issue | resource:unity |
| T358 | #385 | MAQA Issue | resource:unity |
| T359 | #386 | MAQA Issue | resource:unity |
| T360 | #429 | MAQA Issue | resource:unity, resource:emulator |
| T320 | #387 | MAQA Issue | resource:unity |
| T321 | #388 | MAQA Issue | resource:unity |
| T322 | #389 | MAQA Issue | resource:unity |
| T325 | #390 | MAQA Issue | resource:unity |
| T326 | #391 | MAQA Issue | resource:unity |
| T327 | #392 | MAQA Issue | resource:unity |
| T328 | #393 | MAQA Issue | resource:unity |
| T329 | #394 | MAQA Issue | resource:unity |
| T330 | #395 | MAQA Issue | resource:unity |
| T331 | #396 | MAQA Issue | resource:unity |
| T332 | #397 | MAQA Issue | resource:unity |
| T333 | #398 | MAQA Issue | resource:unity |
| T334 | #399 | MAQA Issue | resource:unity |
| T335 | #400 | MAQA Issue | resource:unity |
| T336 | #401 | MAQA Issue | resource:unity |
| T337 | #402 | MAQA Issue | resource:unity |
| T338 | #403 | MAQA Issue | resource:unity |
| T339 | #404 | MAQA Issue | resource:unity, resource:emulator |
| T340 | #405 | MAQA Issue | resource:unity |
| T341 | #406 | MAQA Issue | resource:unity |
| T342 | #407 | MAQA Issue | none |
| T361 | #464 | MAQA Issue | resource:unity |
| T362 | #465 | MAQA Issue | resource:unity |
| T363 | #466 | MAQA Issue | resource:unity |
| T364 | #483 | MAQA Issue | resource:unity |
| T343 | #408 | MAQA Issue | none |
| T344 | #409 | MAQA Issue | resource:unity |
| T365 | #489 | MAQA Issue | resource:unity |
| T366 | #490 | MAQA Issue | resource:unity |
| T345 | #410 | MAQA Issue | resource:unity |
| T346 | #411 | MAQA Issue | none |
| T347 | #412 | MAQA Issue | resource:unity |
| T348 | #413 | MAQA Issue | resource:unity |
| T349 | #414 | MAQA Issue | resource:unity |
| T350 | #415 | MAQA Issue | resource:unity |
| T351 | #416 | MAQA Issue | resource:unity, resource:emulator |
| T352 | #417 | MAQA Issue | none |
| T367 | #501 | MAQA Issue | resource:unity |
| T353 | #418 | owner | none |
| T354 | #419 | owner | none |
| T355 | #420 | parent | none |
| T356 | #421 | parent | none |

---

## Phase 1: Setup and design gates

**Purpose**: Baselines and design records that later work depends on.

- [X] T300 [P] Performance Engineer (Architect lens), plan step 0, #353 Option 0 [U][E]:
  - **Measure** W003-B on `a41c53f` in a separate base worktree. Run the Windows Mono development player on a quiet host (no editor or emulator process during the timed window; record the process list and the window). Take the median of 5 per run over at least 3 runs, record the spread, and attribute time to phases with Stopwatch accumulators (step scan, `CompletionPass`, run predicate, `AutoSell`, lookups).
  - **Emulator**: measure W003-B on the emulator from a development player with the test catalog.
  - **Record**: write `specs/005-economy-contracts/reviews/w003b-measurement.md` with the decision on Option 1 (index-based hot loop) or no change. Option 3 is out of scope.
  - **Gates**: this record gates T316, T325 and T326 (research F5-R02, F5-R18 PF-3). T300 chose Option 1 with a required re-measure gate; T360 implements it.
- [X] T301 [P] Game Systems/Economy (author) with Game Designer/Product Owner review, plan step 13 (design part), writes `specs/005-economy-contracts/reviews/balance-policy-r2.md`. Content:
  - **Reference policy r2 rules (FR-022)**: income as net-worth growth; storage bought on `StorageFull` blockers; IP spent greedily by marginal gain per IP; a balanced machine mix derived from recipe rates; use of `Sell` and auto-deliver.
  - **Strategies**: the full set of FR-023, with the single-lever set {Ore-only, contracts-only, stop-at-Bar, stop-at-Plate}. The casual calibration is a pinned constant.
  - **Ensemble**: 5 look-grid offsets (0, 4, 8, 12, 16 s) and medians per run index; the ECO-3 tolerance is one look interval (research F5-R13, F5-R17).
  - **Orientation**: the outputs reported as orientation only.
  - **No code.**

---

## Phase 2: Foundational (blocks every story)

**Purpose**: The behaviour-preserving refactor, follow-ups, tooling, the strategy framework and content v3.

**⚠️ CRITICAL**: No user-story task starts before T313 has merged, except the [P] tooling tasks named below.

### #356 refactor (FR-036)

- [X] T302 Gameplay Developer (Architect, Test/QA lenses), plan step 1a [U]:
  - **Baseline first**: add `Assets/IdleFactory/Tests/EditMode/Run/PublicApiSnapshotTests.cs` and a committed reflection baseline of the public surface of `IdleFactory.Core` (`Assets/IdleFactory/Tests/EditMode/Fixtures/PublicApi/core-public-api.txt`) in the first commit (QA-16).
  - **Split**: turn `Assets/IdleFactory/Core/Run/Application/RunPersistence.cs` into a `partial class`: lifecycle core and admission table kept whole, plus `RunPersistence.ColdStart.cs`, `RunPersistence.Transition.cs`, `RunPersistenceTypes.cs` (outcome, status and reasons), `OfflineReturn.cs` (`OfflineRunReturnReconciler`, `NoCredit`) and an internal `SafeRunStore.cs`.
  - **Acceptance**: no behaviour change, the snapshot is unchanged, all suites are green (research F5-R01).
- [X] T303 Gameplay Developer (Architect, Test/QA lenses), after T302, plan step 1b [U]:
  - **Split**: `Assets/IdleFactory/Core/Run/Application/RunSnapshots.cs` into `RunSnapshots.Factory.cs`, `RunSnapshots.Economy.cs`, `RunSnapshots.Contracts.cs` and `RunSnapshots.Items.cs` (existing files merged in), plus `RunDerivation.cs`.
  - **Acceptance**: the snapshot is unchanged and all suites are green.
- [X] T304 Gameplay Developer (Architect, Security, Test/QA lenses), after T303, plan step 1c [U]:
  - **Visibility**: make the command-path `RunState` mutators `internal`, deriving the list from a compile (AR-8). Every method used by `SaveV2Mapper` or another restore path stays `public` (`TryAddMachine`, `TrySetAutoSell`, `Inventory.TryImport`, `PermanentState.TryAddItems`, `TryRestoreInvestment`, `TrySetResearchLevel`, `TryAddEffect`, `TrySetIndustryLevel`, `ContractBoard.TryRestoreActive`, `TrySetCompletionCount`).
  - **Test access**: add `InternalsVisibleTo("IdleFactory.PlayModeTests")` in `Assets/IdleFactory/Core/Properties/AssemblyInfo.cs` (or the existing AssemblyInfo).
  - **Acceptance**: the snapshot diff is visibility-only, and Edit Mode, Play Mode and the Windows player suite are green.

### Follow-ups and seams

- [X] T305 [P] Gameplay Developer (Architect, UI/UX lenses), after T304, plan step 2a, #355 [U]:
  - **Contract table**: create `docs/contracts/run-reason-codes.md` with the stable code table from `contracts/run-application-f005.md` (F003 codes plus `command.invalid`, `checkpoint.content` and `discard.unavailable`). F005 codes are added by the tasks that introduce them.
  - **Pin test**: add `Assets/IdleFactory/Tests/EditMode/Run/ReasonCodeContractTests.cs`. It reflects over `RunReasons` and the persistence reason classes, parses the table, and asserts set equality and that `RunRejectionCopy` has copy for each rejection code. A negative test uses an injected unknown code (QA-15).
  - **Links**: point the F003 contract to the new file.
- [X] T306 [P] Gameplay Developer (Security, Architect, Test/QA lenses), after T304, plan step 2b, #354 [U]:
  - **Interval guard**: add a minimum-interval guard to desktop focus-loss safety checkpoints in `RunPersistence` (reading `runStart.liveGapThresholdMs` once T313 has merged; a temporary constant of 60,000 ms before that). Quit and prestige checkpoints bypass the guard (SEC-5).
  - **Exception handling**: lifecycle callbacks catch every exception.
  - **Tests**: add Play Mode tests in `Assets/IdleFactory/Tests/PlayMode/RunDesktopLifecycleTests.cs`. They cover a failing checkpoint write through `RunLifecycleAdapter` (the run stays live, nothing is published, and the next save succeeds), the quit-then-focus-lost order, the interval and the bypasses.
- [X] T307 [P] Gameplay Developer (Architect, Security lenses), after T304, plan step 2c [U]:
  - **Port**: add `Assets/IdleFactory/Core/Run/Application/IRunEventSink.cs` with a no-op default on `RunSession`, and `RunEvents.cs` with readonly allowlisted event structs per the data-model › Session log records.
  - **Constraint**: content IDs and enums only, no strings beyond IDs.
  - **Tests**: the sink is optional and its absence changes nothing (Edit Mode).

### Tooling (D-B; may start after T304 in parallel with T305–T307)

- [X] T308 [P] DevOps/Operations (primary) with Gameplay Developer, Architect and Security lenses, after T304, plan step 3a [U]:
  - **Assembly**: create `Assets/IdleFactory/DevTools/IdleFactory.DevTools.asmdef` (`defineConstraints: ["IDLEFACTORY_DEV"]`, references `IdleFactory.Unity`, never referenced back).
  - **Seam**: add a null-default `ITimeScaleSource` seam in `Assets/IdleFactory/Unity/Bootstrap/FactoryRuntimeHost.cs` with the S_max clamp of 10 simulated seconds per frame. Max = the clamp, and the effective multiplier is reported.
  - **Overlay**: a DevTools overlay (×1/×10/×60/×600/Max, keyboard-operable) and a persistent text marker "ACCELERATED ×N" whose accessible name includes the factor.
  - **Dev save root (D-N)**: `persistentDataPath/IdleFactory-Dev/` under the define.
  - **Builds**: development build variants in `Assets/IdleFactory/Editor/PlatformBuildCommands.cs` use `BuildPlayerOptions.extraScriptingDefines`, never ProjectSettings. Add the new `BuildAndroidArm64Release` (non-development, unsigned for the scan).
  - **Tests**: the clamp and the seam through a fake in Play Mode, and the DevTools UI and dev save root in a separate define-gated test assembly `Assets/IdleFactory/Tests/DevTools/` (AR-10, OPS-1, OPS-3).
- [X] T309 [P] DevOps/Operations (Security lens), after T308, plan step 3a (scan part): create `scripts/check-release-build.ps1`.
  - **Windows**: on a non-development build, check the `Managed/` assembly list for the DevTools assembly.
  - **Android**: on a release APK, extract with `System.IO.Compression` and scan `global-metadata.dat` for DevTools assembly and type names.
  - **Guard**: assert `runInBackground: 0` in `ProjectSettings/ProjectSettings.asset`.
  - **Exit codes**: 0, or 1 with findings. Manifest checks are added by T337 (research F5-R18, SEC-6, OPS-4).
- [X] T310 [P] DevOps/Operations, after T304, plan step 3b: extend `scripts/test-unity.ps1` with `-Category <name>`, mapped to Unity `-testCategory`. Reject it together with `-StandaloneWindowsPlayer` (OPS-2). Verify with one `-Suite EditMode -Category` run [U].
- [X] T311 [P] Gameplay Developer (Game Systems, Test/QA lenses), after T304, D-B strategy framework, #357 part 1 [U]:
  - **Framework**: add `IStrategyPolicy` and a runner extension in `Assets/IdleFactory/Tests/EditMode/Balance/` that runs strategies for runs 1–5 over 5 look-grid offsets with medians per run index.
  - **First strategies**: implement those that need no F005 command (Ore-only, contracts-only on the existing contracts, chain with the current reference policy, idle-heavy, casual with a pinned constant). Record their output on content r2 as a report (no new asserted bands).
  - **Runtime**: measure the runtime per strategy and offset, and project the 9-strategy matrix (PF-15).
  - **Purpose**: this is the Play-Tester's strategy channel from the first gameplay batch.

### Content schema v3 (F5-R03, F5-R17)

- [X] T312 Gameplay Developer (Architect, Game Systems, Test/QA lenses), after T304, plan step 4a [U]:
  - **Definitions**: extend the definitions in `Assets/IdleFactory/Core/Run/Content/` (`RunStartDef`, `ContractOfferDef`, `MachineDef`, `PrestigeDef`, `IndustryUpgradeDef`, `RunCatalog`) with the v3 fields from data-model.md › Content, as optional constructor parameters with legacy defaults. The fields are inert in this task.
  - **Test helper**: add `Assets/IdleFactory/Tests/EditMode/Run/TestCatalogBuilder.cs` so test call sites change once.
  - **Acceptance**: the catalog and builder carry the new fields, all suites are green, and no behaviour changes.
- [X] T313 Gameplay Developer (Architect, Security, Game Systems, Test/QA lenses), after T312, plan step 4b [U]:
  - **Reader**: evolve `Assets/IdleFactory/Unity/Content/RunContentReader.cs` and its DTOs in place to `contentSchemaVersion 3`, strictly. The dispatcher keeps `content.version.unsupported` for other versions.
  - **Transitional reward adapter (analysis I1)**: until T320 replaces it, `RunCatalogBuilder` synthesizes the legacy reward definitions from the v3 fields (Cash table = ceil(quantity × base price × ratio), Item rewards from the ladder), so the F003 delivery path keeps working. T320 removes the adapter.
  - **Shipped content**: migrate both shipped copies to schema v3 with revision 2 values and the F003 offer IDs `contract.plate`, `contract.gear` and `contract.timed-bar`. The copies are `Assets/IdleFactory/Content/Iron/iron-factory-content.json` and `Assets/IdleFactory/Tests/PlayMode/Fixtures/SaveV1/Resources/RunPlayerFixtures/iron-factory-content.json`, plus their parity tests.
  - **Schema and docs**: rename and update `Assets/IdleFactory/Content/Schemas/gameplay-content-v2.schema.json` to `-v3`, and update `docs/content/json-content-pipeline.md`.
  - **Tests**: port the strictness and canonical tests. Give the V2 device sets a frozen r2 catalog (`ShippedDeviceSets`, `DeviceSetShippedCatalogTests`). Prove V1 import on the v3 catalog.
  - **Goldens**: regenerate `trace-r2.csv` once as a labelled transitional golden where outputs change. Conservation and SC-001 gates stay asserted, and F003 pacing bands are reported, not asserted, until T341.
- [X] T314 Gameplay Developer (Game Systems, Security, Test/QA lenses), after T313, plan step 4c [U]: add the structural content rules with rule-code tests in `Assets/IdleFactory/Unity/Content/RunContentValidator.cs`: `sell.steps`; `live-gap.range` (1,000 ≤ value ≤ 28,800,000); `contract.bonus.fields`; `contract.ratio.*` ranges (gated on `contentRevision ≥ 3`); `contract.quantity.cap`; `contract.produce.min`; `milestone.order`; `build.base.bound` (base and cap ≤ 10^12); `cost.floor`; `speed.level.dead`.
- [X] T315 Parent verification gate after T313 and T314 (QA-1) [U]: run the full Play Mode suite and `scripts/test-unity.ps1 -StandaloneWindowsPlayer`, plus a console check. Record the result in the batch notes. A failure blocks Phase 3.

- [X] T357 Play-Tester (read-only; Low tier, haiku: runs existing policies and summarizes), after T315, owner reviews:
  - **Run**: the T311 strategy framework on the transitional v3 content (revision 2 values through the reward adapter) against the r2 report.
  - **Report**: `specs/005-economy-contracts/reviews/playtest-agent-batch-1.md` with the deviations per strategy and any changed reward outcomes (analysis C1; AGENTS.md Play-Tester gate for batch 1).

- [X] T360 Gameplay Developer (Performance, Architect, Test/QA lenses; Medium at `high` reasoning), after T315 and T300, plan step 5 (Option 1 part, F5-R02) [U][E]:
  - **Hot loop**: add the index-based Option 1 path chosen by T300 (`reviews/w003b-measurement.md` › Decision and Scope of Option 1) as an additional API in the completion pass of `RunSimulator`: precomputed index and amount arrays for `UnitsThatFit` and `TryExchange`, no interface enumeration, a fused exchange. The command path keeps its semantics.
  - **Limits**: no skipped timestamps and no closed-form batching of completions (that is Option 3, out of scope). Passes per timestamp and their ordering stay unchanged.
  - **Acceptance**: bit-identical results against the committed W003 fixtures and hashes; PF-1 pass numbers unchanged; full Edit Mode, Play Mode and Windows player suite green.
  - **Re-measure (PF-3)**: repeat the T300 protocol with the same probe harness on a quiet host (Windows and emulator) and append it to `specs/005-economy-contracts/reviews/w003b-measurement.md`. If the clean Windows median is still above 500 ms, the parent records an owner decision before T326 (escalation path in the T300 record).

**Checkpoint**: Foundation ready. The user-story phases may begin. T360 must be merged before T316 and T326.

---

## Phase 3: User Story 1 — Sell stock by hand (P1) 🎯 MVP

**Goal**: The player sells chosen quantities at the same price as auto-sell, with no Factory Value gain (FR-001–FR-003).

**Independent Test**: With auto-sell off, sell 1, 10, 100 and All. Check exact Cash and stock deltas, no partial change on rejection, and full-stock FV neutrality.

- [X] T316 [US1] Gameplay Developer (Game Systems, Performance, Architect, Test/QA lenses; Medium at `high` reasoning), after T315 and T360, plan step 5 [U]:
  - **Pricing**: add `Assets/IdleFactory/Core/Run/Domain/SalePricing.cs` (`MulDivFloor(units × base, salePricePpm, 10^6)`, checked) and route `RunSimulator.AutoSell` (one sale per resource per instant, after the instant's expiries) and `FactoryValuation` through it.
  - **Rule**: add the `chain.dominance` rule (efficiency monotone along the chain and terminal > Ore × (1 + margin), revision ≥ 3) with tests.
  - **Tests**: `SalePricingTests` and the equivalence case (q) for conservation per step.
  - **Goldens**: name every golden regenerated.
- [X] T317 [US1] Gameplay Developer (Game Systems, Test/QA lenses), after T316, plan step 5 [U]:
  - **Commands**: add `Sell(resourceId, quantity)` and `SellAll(resourceId)` in `Assets/IdleFactory/Core/Run/Application/RunCommands.Sell.cs`. The order is lifecycle, `resource.locked`, `sell.quantity-invalid`, `stock.insufficient`, then `value.limit`. The `SettleFits` pre-check subtracts the sold stock.
  - **Snapshot and events**: add `SellSteps` and `SellPreview` to `RunSnapshots.Economy.cs`, and raise the sale events through `IRunEventSink`.
  - **Contract table**: add `sell.quantity-invalid` to `docs/contracts/run-reason-codes.md`.
  - **Tests** (`RunSellCommandTests`):
    - each rejection, with no partial change;
    - full-stock FV neutrality, and no sale raising FV;
    - sell during a full-store block (QA-5);
    - 10^18 −1, = and +1 boundaries (QA-6).
- [X] T318 [US1] Performance Engineer, after T317: re-measure W003-B (auto-deliver does not exist yet) on the same hashes, apply the 10 % regression guard against the post-#356 baseline, and append the result to `reviews/w003b-measurement.md` (PF-3, PF-10) [U].
- [X] T319 [US1] UI/UX Developer (UI/UX Reviewer, Game Designer, Test/QA lenses), after T317, plan step 14a [U]:
  - **UI**: in `MarketPanelHost` and the inventory row presenter under `Assets/IdleFactory/Unity/UI/`, add the step selector (`inventory-sell-step-<resourceId>`) and the Sell button (`inventory-sell-<resourceId>`) per `contracts/ui-surfaces-f005.md`.
  - **States**: "Nothing to sell" or "Locked: unlocks after <condition>" rows; "Need N more" without silently changing the step; the All confirmation only when an active delivery contract would fall short; a success notice with the exact value `N × $p = $Y`; re-render on stale taps with focus kept.
  - **Focus**: per-row focus order step → Sell → Auto-sell.
  - **Names**: accessible names per the name pattern table.
  - **Tests**: `MarketSellPanelTests` covering the control × state × screen-class rows for Sell in the QA-12 table below.

**Checkpoint**: US1 is independently functional.

---

## Phase 3b: User Story 10 — Mine ore by hand (P1)

**Goal**: An ore vein in the Factory view produces Ore by click, tap, keyboard or hold, with a rate cap and an upgradable yield. It makes the first Miner of every run reachable with constant starting Cash (FR-040–FR-044, FR-016, SC-011, D-J revised, D-O).

**Independent Test**: In a fresh run and at a high prestige index, mine by clicks and by holding. Ore per counted activation is exact, the cap ignores excess, a full store rejects, and the first Miner is affordable within 60 s.

- [X] T358 [US10] Gameplay Developer (Game Systems, Security, Test/QA lenses), after T317 and T323, plan step 9b [U]:
  - **Command**: add `Assets/IdleFactory/Core/Run/Application/RunCommands.Mining.cs` with `Mine(activations)` (lifecycle → rate cap → `storage.full` → `value.limit` → checked add → settle). The result reports counted, ignored and units.
  - **Rate counter and remainder**: a transient `(TimelineSecond, CountedInSecond, RemainderMilli)` on `RunState`, internal, not persisted. Milli-yield per D-P, counted as `min(n, cap remaining, activations whose whole Ore fit)`; rejected activations do not use the cap (MIN-10).
  - **Effect**: the `manualYield` effect kind in the closed effect vocabulary (Research and Industry; Industry decay as a multiplicative kind).
  - **Content**: `runStart.manualMining` read in the content v3 reader and DTOs.
  - **Rules**: `manual.yield.bound` and `start.reachable` (revision ≥ 3, no discounts, every index ≤ `maxIndex`) in `RunContentValidator.cs`.
  - **Codes and events**: add `storage.full` to `docs/contracts/run-reason-codes.md`, and raise aggregated mining events through `IRunEventSink`.
  - **Tests** (`ManualMiningTests`):
    - ten activations of 100 milli giving exactly 1 Ore;
    - the cap with 50 activations in one second giving 10, and the cap across a second boundary;
    - partial counting at a full store, with the cap not consumed;
    - the 10^18 boundary and lifecycle-blocked;
    - the yield effect with no dead level;
    - mined Ore not advancing a production objective (MIN-5);
    - both rules with negative content cases, and a first-Miner reachability probe at `maxIndex`.
- [X] T359 [US10] UI/UX Developer (UI/UX Reviewer, Game Designer, Test/QA lenses), after T358, plan step 14e [U]:
  - **Vein**: add `factory-ore-vein` as a screen-space overlay in the Factory view under `Assets/IdleFactory/Unity/UI/`, per `contracts/ui-surfaces-f005.md` and research F5-R20 (VEIN-1 to VEIN-12). It sits outside the slot rectangle and is hidden when a sheet covers it.
  - **Input**: pointer click, touch tap, keyboard Space/Enter, and hold of each, with pointer capture. Hold accrual: the first press counts immediately, then one activation per 1/cap s of run timeline, with at most one catch-up per frame. Keyboard: KeyDown and KeyUp, no OS repeat, de-duplicated with NavigationSubmit. The hold ends on release, leaving the vein, a second pointer, rotation, focus loss or a dialog.
  - **Feedback**: one pooled pulse with a static fallback and reduced-motion handling; a progress indicator in tenths; a "max rate" cue; "Store full" clearing without a layout jump; no per-click screen-reader announcement. The vein is excluded from exact-value gestures, and the focus order is after the HUD strip.
  - **Accessibility**: at least 44 pt, with the accessible name "Mine Iron Ore, +0.1 per click" (exact).
  - **Tests**: `OreVeinPanelTests` per the QA-12 row, covering:
    - a hold across rotation, focus loss and a second pointer granting no extra Ore;
    - a 600 ms touch hold opening no peek;
    - keyboard repeat adding nothing beyond the cap;
    - no overlap with slots, the strip, the tab bar or the sheet at every screen class, at 100 % and 200 %, in both rotations.

**Checkpoint**: US10 works, and the first Miner of every run is reachable by hand.

---

## Phase 4: User Story 2 — Contracts are a choice, not a substitute (P1)

**Goal**: Ratio rewards with headroom, cooldowns, bonus offers, production objectives and one Item ladder (FR-004–FR-009, FR-007a).

**Independent Test**: For every offer and modifier combination, the reward is within its bounds. Cooldown follows every ending. A production objective is not advanced by trade. Ladder grants follow the run-wide index.

- [X] T320 [US2] Gameplay Developer (Game Systems, Architect, Test/QA lenses), after T317, plan step 6a [U]:
  - **Adapter removal**: remove the T313 transitional reward adapter, together with `ContractRewardDef.FromTable` and its public API baseline line. `RunCatalogBuilder` sorts ladder entries by (index, itemId).
  - **Goldens (AR2-2)**: the switch from the adapter's ceil to `MulDivFloor`, together with the run-wide ladder index, shifts contract Cash and Item timing. Regenerate `trace-r2.csv` once as a labelled transitional golden and update `trace-r2-transitional.md`.
  - **Rewards**: add `Assets/IdleFactory/Core/Run/Domain/ContractRewards.cs` with `Grant` and `CheckGrant`. The Cash reward is `MulDivFloor(SalePricing.Value(required, basePrice, salePricePpm), rewardRatioPpm, 10^6)`, with modifiers read at the completion instant (FR-009).
  - **Delivery path**: move `RunState.TryDeliverContract` onto it.
  - **Item ladder**: grant when the pre-increment run-wide completion index equals the entry index, up to `perRunCap`. Production completions count.
  - **Completion total**: keep `runCompletionTotal` in `ContractBoard`.
  - **Rules**: add `contract.quantity.capacity`, `contract.ratio.produce` (R_prod + max standing R for the resource ≤ 2.0) and `ladder.uptime` (`perRunCap × duration ≤ uptimeMaxPpm × minRunDurationMs`).
  - **Tests**: `ContractRewardsTests`, including Research bought between accept and delivery (ECO-2), the 1024-index bound and the reward 10^18 boundary.
- [X] T321 [US2] Gameplay Developer (Game Systems, Test/QA lenses), after T320, plan step 6b [U]:
  - **Board**: in `Assets/IdleFactory/Core/Run/Domain/ContractBoard.cs`, add cooldowns started on every ending (delivery, abandon, expiry) as UTC stamps evaluated lazily, bonus-offer state (no trigger yet), the `Version` counter included in `RunSession.Publish` tokens, and the FR-007a prestige reset.
  - **Accept**: `AcceptContract` checks `contract.cooldown`, `contract.bonus.unavailable` and `contract.capacity` (required > current capacity).
  - **Snapshot**: add the fields per the contract (`Objective`, `Class`, `CooldownRemainingMs`, `BonusWindowRemainingMs`, `Deliverable`).
  - **Contract table**: add the three codes to `docs/contracts/run-reason-codes.md`.
  - **Tests**:
    - `ContractCooldownTests`: 3 endings × live and offline × exact `readyAt` (QA-9);
    - `BonusOfferTests` (state);
    - prestige reset cases: cooldown running, auto-deliver on, produced, bonus triggered (QA-8).
- [X] T322 [US2] Gameplay Developer (Game Systems, Test/QA lenses), after T320, plan step 6c [U]: add production-objective state (`Produced`) and command paths. `DeliverContract` returns `command.invalid` for production objectives, and `AbandonContract` starts the cooldown. Tests in `ProduceObjectiveTests`: trade never advances the objective (completion counting comes in T327).

**Checkpoint**: US2 contract rules work at the command level. Auto-completion comes with US3.

---

## Phase 5: User Story 4 — Prices grow with the factory (P1)

**Goal**: Build prices scale with the owned count and the number of completed prestiges, starting Cash stays constant, and the displayed price equals the charged price (FR-013–FR-015, FR-017).

**Independent Test**: Build successive machines, remove and rebuild, and prestige. Every shown price equals the oracle and the charged price, and Cash + FV never rises from remove/rebuild.

- [X] T323 [P] [US4] Gameplay Developer (Game Systems, Test/QA lenses), after T315, plan step 8 [U]:
  - **Pricing**: add `Assets/IdleFactory/Core/Run/Application/BuildPricing.cs` with the algorithm of research F5-R06: early cap return, per-step `MulDivFloor`, a final ceiling, then the F003 discounts with round-up.
  - **Command and cache**: `Build(slot, machineId, long? expectedPrice = null)` rejects with `build.price.changed` on a mismatch. Cache the price per structure revision in `RunSnapshots.Factory.cs`.
  - **Rules**: `build.price.cap`. The first-Miner check is `start.reachable` in T358.
  - **Contract table**: add `build.price.changed` to the reason table.
  - **Tests**: `BuildPricingTests` with a BigInteger oracle and a random-parameter property test, ECO-7 remove/rebuild, and the 10^18 build boundary.
- [X] T324 [US4] UI/UX Developer (UI/UX Reviewer, Test/QA lenses), after T323, plan step 14c (build part) [U]:
  - **Build sheet** under `Assets/IdleFactory/Unity/UI/`: `build-price-<machineId>` with the price (exact on inspect), "Owned: N" and "Capped".
  - **Recovery**: "Price changed to $Y" with no charge.
  - **Remove**: the confirmation states the rebuild price.
  - **UI passes the price**: the UI always passes `expectedPrice`.
  - **Tests**: `BuildPricePanelTests` per the QA-12 rows.

**Checkpoint**: US4 is independently functional.

---

## Phase 6: User Story 3 — Auto-deliver a contract (P2)

**Goal**: Auto-delivery and production completion inside the simulator, segment-invariant, with a reserve for every active delivery contract (FR-010–FR-012, D-L).

**Independent Test**: Live and offline, single and partitioned advances give identical delivery instants and states. The reserve protects the stock, and an overflow blocks without change.

- [X] T325 [US3] Gameplay Developer (Architect, Performance, Test/QA lenses; Medium at `high` reasoning), after T321, T322 and T300, plan step 7a-1 [U]:
  - **Mapping**: add `Assets/IdleFactory/Core/Run/Simulation/TimelineMapping.cs` (origin ms, sub-ms tick phase, `num/den`, a single floor rule).
  - **Wiring**: route `RunSession.TimelineUtcMs`, `OfflineSegmenter` and `RunSimulator.Advance(activeTicks, mapping, boundaryEvents)` through it, keeping a legacy overload. The mapping is called per advance and per stamp, never per completion (PF-6).
  - **Acceptance**: bit-identical results against the W003 fixtures and hashes, with live and offline stamps asserted (AR2-1).
- [X] T326 [US3] Gameplay Developer (Architect, Game Systems, Performance, Test/QA lenses; Medium at `high` reasoning), after T325 and T360, plan step 7a-2 [U]:
  - **Scratch state**: in `RunSimulator`, add scratch `_rp`, contract state and Item grants, all committed at the end of the advance.
  - **Settle loop**: completions → auto-delivery (at most once per timestamp) → auto-sell keeping `min(stock, required)` for every active delivery contract (D-L).
  - **Pass budget**: N + 2, fail-closed at the bound with a diagnostic (PF-1).
  - **Expiry**: the deadline boundary is at deadline + 1 ms (one timeline millisecond, as in F003; owner decision 2026-10-15, #391).
  - **Blocked delivery**: `value.limit` blocks with no change, sets the blocked state in the snapshot, and retries at later instants. The event fires only on state change (AR2-4).
  - **Command**: add `SetAutoDeliver` with the `DeliverContract` worst-case pre-check.
  - **Tests**: (r) `SetAutoDeliver` delivers at t = 0, and (s) a block retries.
- [X] T327 [US3] Gameplay Developer (Architect, Game Systems, Test/QA lenses), after T326, plan step 7b [U]:
  - **Production**: count production in the completion pass (O(1)).
  - **Bonus**: trigger in the unlock pass, stamping the window end as `RealUtc(t) + window`.
  - **Availability**: cooldown and bonus availability are evaluated lazily, with no boundary events.
  - **Outputs**: add `LastOutcome` and offline-summary fields (`AutoDelivered`, `ProduceCompleted`, `Expired`, reward totals), and session events from outcome and board-version diffs.
  - **Tests**: production completion and the once-per-run bonus.
- [X] T328 [US3] Test/QA-minded Gameplay Developer (Architect, Test/QA lenses), after T327, plan step 7c [U]: in `Assets/IdleFactory/Tests/EditMode/Run/AutoDeliverEquivalenceTests.cs`, add equivalence cases (l)–(s) from plan › Event order:
  - auto-delivery across live partitions, including deadline ±1 tick and sub-ms phase carry;
  - production across partitions;
  - the reserve;
  - identical cooldown and bonus stamps;
  - live gap equivalence (placeholder until T337);
  - conservation;
  - t = 0 delivery;
  - a blocked retry.
- [X] T329 [US3] Performance Engineer, after T325, T326 and T327: re-measure W003-B with and without auto-deliver after each of T325, T326 and T327 on the same hashes. Record the realized maximum passes per timestamp and append to `reviews/w003b-measurement.md` (PF-1, PF-3) [U].
- [X] T330 [US3] UI/UX Developer (UI/UX Reviewer, Game Designer, Test/QA lenses), after T327, plan step 14b [U]: in the contracts panel and the shell tab under `Assets/IdleFactory/Unity/UI/`, add per `contracts/ui-surfaces-f005.md`:
  - `contracts-autodeliver` (delivery objectives only, Off after accept, hint "applies to this contract only");
  - the auto-delivered notice and the short-stock expiry line;
  - `contracts-offer-cooldown-<offerId>` on the once-per-second tick, with a bonus-ended transition that does not jump the layout;
  - `contracts-produce-progress`;
  - the tab label "Contracts (n)" or "Contracts !" where space allows, and `tab-contracts-badge` (a separate element that exists only in the compact dot form "•"; accessible name "Contracts, n deliverable"), cleared when nothing is deliverable;
  - Accept rejection copy for `contract.cooldown`, `contract.bonus.unavailable` and `contract.capacity`.

  Tests: `ContractsAutoDeliverPanelTests` per the QA-12 rows.

**Checkpoint**: US3 is functional live and offline.

---

## Phase 7: User Story 5 and User Story 6 — Next goal and stronger runs (P2)

**Goal**: A rising threshold clamped at `maxIndex`, display-only milestones, and Industry with diminishing returns (FR-018–FR-021).

**Independent Test**: The threshold per index equals the oracle and stays constant beyond `maxIndex`. The goals snapshot covers every state. The Industry tables match the per-kind decay. A retry advances the index once.

- [X] T331 [P] [US6] Gameplay Developer (Game Systems, Architect, Test/QA lenses), after T315, plan step 9 [U]:
  - **Threshold**: add `Assets/IdleFactory/Core/Run/Application/PrestigeThreshold.cs`, iterated floors with a final ceiling. The index is `min(PrestigeCount, maxIndex)` (D-K). Preview and confirm read it, and `PrestigePreview` adds `Threshold`, `NextThreshold` and `PrestigeIndex`.
  - **Industry**: per-kind diminishing tables in `Assets/IdleFactory/Core/Run/Content/IndustryUpgradeDef.cs` (multiplicative increment, discount decrement, additive amount; the cumulative fold keeps the `FoldLevels` floor).
  - **Rules**: `prestige.threshold.bound` and `industry.ceiling.level`.
  - **Tests**: `PrestigeThresholdTests` (oracle; a retry or interruption advances the index once) and `IndustryDiminishingTests`.
- [X] T332 [US5] Gameplay Developer (Game Systems, Test/QA lenses), after T331, plan step 9 (goals): add `Assets/IdleFactory/Core/Run/Application/RunSnapshots.Goals.cs` (`GoalsSnapshot`: next milestone or the threshold when content defines none, target, reached, floored percentage, Factory Sale available, all reached). The cache is keyed on milestone crossing (PF-8). Raise the milestone event. Tests in `GoalsSnapshotTests` [U].
- [X] T333 [US5] UI/UX Developer (UI/UX Reviewer, Test/QA lenses), after T332 and T331, plan step 14c (goals and prestige part) [U]:
  - **Goal line**: `goals-next` at the top of Market (Market-only, a known limitation for F004) with a one-time notice.
  - **Prestige panel**: `prestige-threshold` shows the current and next threshold and the index.
  - **Tests**: `GoalsLinePanelTests` per the QA-12 rows.

**Checkpoint**: US5 and US6 are functional.

---

## Phase 8: Save V3 (persistence for US2, US3 and US6)

**Goal**: Save V3 reads V2, strict and fail-closed, with contract-scoped content drift (FR-034, SC-006).

**Independent Test**: Every V2 fixture loads into an equivalent V3 state. Every malformed V3 fixture is rejected without change.

- [X] T334 [US3] Gameplay Developer (Architect, Security, Test/QA lenses), after T327, plan step 10a [U]:
  - **Mapper**: version-parametrize `Assets/IdleFactory/Unity/Persistence/SaveV2Mapper.cs` with per-version member tables chosen at the `schemaVersion` peek. `ValidateBytes` checks the version read.
  - **Writer**: always writes V3, only after a successful load or a fresh start.
  - **V3 members**: `activeContract.autoDeliver`, `activeContract.produced`, `contractCooldowns[]`, `bonusOffers[]` (`windowEndUtcMs` required for both states) and `runCompletionTotal`.
  - **Restore setters**: `ContractBoard` `TryRestoreCooldown`, `TryRestoreBonus` and `TryRestoreProduced`, public, with the API baseline regenerated and architect-reviewed.
  - **Rename**: an optional rename to `SaveMapper` happens only as a separate mechanical commit.
- [X] T335 [US3] Gameplay Developer (Security, Test/QA lenses), after T334, plan step 10b [U]:
  - **Content drift**: drop only contract-scoped entries (active, cooldowns, bonus, `completionCounts`) of unknown offers and report `contract.dropped`. `runCompletionTotal` is preserved, and a test proves no Item is re-granted (SEC-8).
  - **Fail-closed matrix**: per the QA-11 table below.
  - **V2 regression**: every existing V2 fixture (device sets, W003, `prestige-ready`, import outputs).
  - **Fault injection**: a failed first V3 write after a V2 load keeps V2 readable.
  - **Forward compatibility**: `schemaVersion` 4 is refused without any write or V1 import (SEC-14).
  - **Contract table**: add `contract.dropped`.
  - **Tests**: `SaveV3*Tests` and fixtures under `Assets/IdleFactory/Tests/EditMode/Fixtures/SaveV3/`.
- [X] T336 [US3] Performance Engineer (Game Systems lens), after T335 and T331: create the `W005-L` Save V3 fixture near the run-3 threshold (12 slots, chain mix, auto-deliver active, auto-sell on end products) with its hash, plus a StandalonePlayer `Resources` copy and a SHA-256 parity test. Take an early Windows timing on a quiet host recording completions, timestamps, passes per timestamp, V3 write time and size. Record in `reviews/w005l-measurement.md` (QA-2, PF-11, PF-16) [U].

---

## Phase 9: User Story 9 — Waking from sleep is an offline return (P2)

**Goal**: A wall gap above the threshold becomes a standard offline return (FR-032, FR-033).

**Independent Test**: A gap above the threshold gives the same result as a quit and restart. Gaps at or below the threshold run live. A negative gap credits nothing.

- [X] T337 [US9] Gameplay Developer (Architect, Security, Test/QA lenses), after T306, T308, T325 and T334, plan step 11 [U]:
  - **Clock**: an explicit UTC read in `FactoryRuntimeHost`.
  - **Suspension**: a `RunPersistence.RecordSuspension(atUtcMs)` overload through `Admit`, including a pending prestige. The departure is `max(max(timelineEnd, activeDeparture), min(atUtcMs, clock))`: the timeline is the floor, so the credit is 0 when a development-accelerated timeline is ahead of the clock, and the departure is never later than the current UTC read (SEC-10). Implemented and architect-approved in #402.
  - **Gap path**: `ResumeAndReconcile` follows. Only the gap path calls the overload.
  - **Time scale**: applied after the gap check.
  - **Tests** (`LiveGapTests`): threshold −1, = and +1 ms; a negative clock; accelerated frames not counting; a checkpoint inside a gap; a gap during a pending prestige; the equivalence case (p) in `AutoDeliverEquivalenceTests`.

---

## Phase 10: User Story 8 — Local session log (P3)

**Goal**: Allowlisted, bounded, failure-isolated local log with export, toggle and clear (FR-028–FR-031, SC-008, D-M).

**Independent Test**: A session with a sale, a contract, a prestige and an offline return exports a valid log within the bound, containing only schema fields. A forced write failure leaves play and saving unaffected.

- [X] T338 [US8] Gameplay Developer (Security, Performance, Test/QA lenses), after T307 and T335, plan step 12b [U]: add `Assets/IdleFactory/Unity/Persistence/SessionLogWriter.cs` and `SessionLogExport.cs`.
  - **Injection**: an injected root, `ISessionLogPrefs` (`PlayerPrefs` key `IdleFactory.SessionLog.Enabled`, default on) and an injected file layer.
  - **Format**: in-memory ring buffer; two ~128 KB segments of CRC32 JSON lines from a hand-written serializer (no reflection JSON); a torn last line is skipped.
  - **Flush**: at lifecycle points and at most every 30 s, off the frame path.
  - **Isolation**: try/catch everywhere, a drop counter, and Clear, toggle and flush serialized.
  - **Export**: a temp file then rename, at most 10 files kept, path shown on all platforms (D-M).
  - **Samples**: from the host at 10, 15, 30, 60, 90 and 120 min, and at milestones.
  - **Session id**: from `RandomNumberGenerator`.
  - **Tests**:
    - `SessionLogSchemaTests`: the allowlist and a writer-output check that every string is a content ID or enum token (SEC-1);
    - isolation;
    - 0 `GC.Alloc` in the session wrapper with logging on;
    - flush allocation and maximum-milliseconds budget (PF-7).
- [X] T339 [US8] DevOps/Operations (Security lens), after T338 and T309, plan step 12c [U][E]:
  - **Backup rules**: add Android backup rules (`fullBackupContent` and `dataExtractionRules` XML excluding `SessionLog/` and `Exports/`) to the existing `Assets/Plugins/Android/AndroidManifest.xml` and `res/xml`. No FileProvider and no Gradle template (D-M).
  - **Release check**: extend `scripts/check-release-build.ps1` with the merged-manifest check (`allowBackup`, both attributes, the XML exclusions; via `Temp/gradleOut` or `aapt2` with the SDK path from config).
  - **Build check**: build the GLES3 emulator APK and confirm it still runs.
  - **Settings**: `git diff ProjectSettings/` is empty except for deliberate changes (OPS-6).
- [X] T340 [US8] UI/UX Developer (UI/UX Reviewer, Security, Test/QA lenses), after T338 and T308, plan step 14d [U]:
  - **Session-log section**: add `session-log-section` as the fourth `MarketPanelHost` section with the toggle, export, clear (confirmation "Clear the session log? This cannot be undone", cancelled by rotation) and a persistent "Last export" line (selectable on Windows, never logged).
  - **DevTools overlay**: polish for keyboard use and safe areas. Owner addition 2026-10-21 (#405, F6 gate on #408): the time-scale row (×1…Max, `DevToolsOverlay.Build`) must never overlap `factory-ore-vein` (Mine) or another shell control at any supported screen class; in 16:9 windows (1280×720, 1920×1080) it covered Mine completely. Place it for example at top centre below the HUD or in a free corner within the safe area, and add a PlayMode test that its world bound does not overlap `factory-ore-vein` at the QA-12 screen classes.
  - **Auto-deliver flag (F1 from #395, owner decision 2026-10-21, #405)**: the expired-contract snapshot carries `AutoDeliverWasOn`, persisted with the snapshot and validated fail-closed on load. The short-stock expiry line reads it instead of panel memory and survives a restart and an offline return. Tests cover both.
  - **Session-log accessor**: a bootstrap accessor (or injected interface) to the session log for `SetEnabled`, `Clear` and `SessionLogExport.Export`; the writer's `_ioGate` stays the only path, and the export path is shown in the UI and never logged. If the offline summary sheet is touched, show `RunOfflineSummary.ContractDropCode` as informational text.
  - **Tests**: `SessionLogPanelTests` per the QA-12 rows.

---

## Phase 11: User Story 7 — Strategy trace, balance r3 and time acceleration (P2)

**Goal**: Policy r2 and the full strategy matrix with asserted gates, tuned content revision 3, and Play-Tester reports per batch (FR-022–FR-027, SC-002, SC-004, SC-010).

**Independent Test**: `BalanceFull` runs within its budget, reproduces, and passes the FR-024 gates on medians. Acceleration is marked and absent in release.

- [X] T341 [US7] Gameplay Developer (Game Systems, Test/QA, Performance lenses), after T301, T311, T317, T327, T323, T331, T358 and T310, plan step 13 [U]:
  - **Policy and strategies**: implement reference policy r2 per `reviews/balance-policy-r2.md` and the remaining strategies (stop-at-Bar, stop-at-Plate, market-only chain, prestige-later, chain+mining, mining-only) in `Assets/IdleFactory/Tests/EditMode/Balance/`. The chain+mining gate uses `miningAdvantageMaxPpm`, and the mining income share is reported (MIN-9).
  - **Ensemble and gates**: the 5-offset ensemble (reduced if the PF-15 projection exceeds 20 min) and the FR-024 gates in `BalanceTraceTests` (fast gate: chain and Ore-only, 1 offset, runs 1–2) and `BalanceTraceFullTests` (`[Category("BalanceFull")]`).
  - **4.0× rule**: the trace gate over the max IP of the at-threshold and prestige-later strategies.
  - **Price variant**: the non-rounding variant replaces `var.price-80`, with a test that the old path is gone (QA-14).
  - **Output**: `trace-r3.csv`, regenerated only on opt-in.
  - **Transitional golden (T313)**: restore the asserted pacing bands, or replace them with the r3 FR-024 gates. Turn the `Reported(...)` items in `BalanceTraceTests` back into assertions, and delete `trace-r2-transitional.md`.
  - **Casual pin (T311)**: the casual look interval of 73 s is provisional because it was found on policy r1 without mining. Pin it once more on policy r2 with the held mining click (balance-policy-r2 § 7.1). Re-measure the PF-15 projection on r2.
- [X] T342 [US7] Play-Tester (read-only; Medium, sonnet) for gameplay batch A (after US1, US10, US2 and US4 merge: T319, T359, T322, T324), owner reviews:
  - **Strategy runs**: through the T311 framework.
  - **Player session**: a session in a Windows build with screenshots.
  - **Report**: write `specs/005-economy-contracts/reviews/playtest-agent-batch-a.md` with measurements and labelled feel inferences. Each finding has evidence, severity and a proposed disposition (SC-010).
- [X] T361 [US7] Gameplay Developer (Test/QA lens), after T342, T308 and T359, batch-A Play-Tester defects F7 and F6 (owner decision 2026-10-15, #408) [U]:
  - **Time overlay (F7)**: the DevTools time-scale overlay renders and operates in the Windows and Android development builds. Give its `PanelSettings` a theme style sheet (warning at `DevToolsOverlay.cs:67`), and show the ×1/×10/×60/×600/Max controls (keyboard-operable) and the "ACCELERATED ×N" marker whose accessible name includes the factor, as T308 specifies. Asset changes only through Unity tooling. DevTools never reach release builds.
  - **Mine hold (F6)**: a Play Mode test holds the Mine control with real pointer input (down, wait, up) and asserts the hold yield of US10 scenario 3 (FR-042). If it fails, fix the mining input path within the T359 surface.
  - **Tests**: the overlay has a non-zero layout and a visible marker in the define-gated `Assets/IdleFactory/Tests/DevTools/` assembly; the Mine hold test in `Assets/IdleFactory/Tests/PlayMode/`.
- [X] T362 [US7] UI/UX Developer (UI/UX Reviewer, Test/QA lenses), after T342, T319 and T333, batch-A Play-Tester F8 and F9 and owner decisions on #382 and #417 (2026-10-15) [U]:
  - **Shell focus (F8, #417(c))**: `RunShellDocument.OnFocusIn` no longer throws `ArgumentException` when the focused element is outside the ScrollView content container (guard the `ScrollTo`). `FocusWhenReady` stops retrying once focus has moved elsewhere. Play Mode regression tests cover a tab switch followed by scroll-bar clicks.
  - **Labels (F9)**: the store fill bar reads "Store", the Overdrive control reads "Overdrive" next to its state, and the top-bar currency reads "Cash", as visible text and accessible name, at every screen class and at 200 % text.
  - **Sell rows (#382)**: locked rows show "Locked: unlocks after <condition>" (`contracts/ui-surfaces-f005.md`) instead of being hidden, and "Need N more" shows the shortfall ("Need 5 more").
  - **Tests**: extend `MarketSellPanelTests` and the shell tests per QA-12; the console stays free of the F8 exception.
- [X] T363 [US7] Game Systems/Economy (author) with Game Designer/Product Owner review, after T342, T321 and T322, batch-A Play-Tester F2 and F4 (owner decision 2026-10-15, #410) [U]:
  - **Content slice**: in `contentRevision` 2 of the shipped content (every shipped copy T345 names; the frozen Save V2 catalog `iron-factory-content-r2.json` stays unchanged), give one standing delivery offer a non-zero cooldown, add one bonus offer and one production-objective offer, all passing the existing validator rules. No other tuning: reward ratios (F3), prices and dominance stay for T345.
  - **Goldens**: regenerate or relabel the transitional balance golden as AR2-2 and T313 prescribe, and record what changed and why.
  - **Runs**: full Edit Mode and Play Mode; catalog-derived tests stay green.
- [X] T364 [US8] Gameplay Developer (Security, Performance, Test/QA lenses; Medium at `high` reasoning), after T338 and T337, before T343, T338 review scope (owner decision 2026-10-21, #403) [U]:
  - **Core raise sites**: through the existing `IRunEventSink` structs (T307), main thread only, raise Prestige, Purchase (research, Industry, machine and storage upgrades), Bottleneck (per-machine wait seconds, research:252) and the contract commands Accept, Deliver and Abandon.
  - **Clock events**: Clock records `liveGap` (from T337's gap path) and `forwardJump`, as the spec and research say the session log records clock events.
  - **Flush**: flush immediately after an offline return and after a prestige (the contract's "lifecycle points"; T338 QA W1).
  - **T338 review items**: a test of the bootstrap live-gap wiring (`IsLiveGapReturn` to `reclassifiedLiveGap`); a test that every shipped content ID passes `SessionLogIds.IsValid`; `OpenSegments` reads only the segment tail; a one-line definition of Sample `playSeconds` (counts from the session's first sight of the run) in `data-model.md` and `contracts/session-log.md`.
  - **Acceptance**: the US8 Independent Test (a session with a sale, a contract, a prestige and an offline return exports a valid log within the bound, schema fields only) passes end-to-end, and saves stay byte-identical with logging on and off.
- [X] T343 [US7] Play-Tester (read-only; Medium, sonnet) for gameplay batch B (after US3, US5, US6 and US9 merge: T330, T333, T337, and after T361, T362, T363 and T364), owner reviews:
  - **Strategy runs and session**: strategy runs plus an accelerated development-player session (marker visible in screenshots).
  - **Report**: `reviews/playtest-agent-batch-b.md`.
- [X] T344 [US7] Gameplay Developer (Test/QA, UI/UX lenses), after T319, T324, T330, T333, T340 and T359, plan step 15, QA-1 [U]: convert remaining hard-coded content values in the Play Mode panel and journey tests under `Assets/IdleFactory/Tests/PlayMode/` to catalog-derived values. Journeys assert shape (run 2 not slower, threshold rises), never seconds.
- [X] T365 [US7] UI/UX Developer (UI/UX Reviewer, Test/QA lenses; Medium), after T344 and T362, before T352, batch-B Play-Tester defect F-B3 (owner decision 2026-10-22, #408) [U]:
  - **Cause first**: isolate why a selected machine shows stale or wrong guidance: a running Smelter at 1 % store showed "Storage is full: one cycle needs room for 2 more units, only 1 free." with the "Auto-sell Iron Ore" and "Upgrade storage" buttons for at least 2 s, and a running Miner showed "Missing Iron Plate: needs 2, has 1. Build a Press to make Iron Plate." (`reviews/playtest-agent-batch-b.md` §4, screenshots 09 and 11). Candidates: a panel left over from the previously selected slot, or a blocked-reason source that is not the selected machine's current state.
  - **Fix**: the machine panel shows guidance and fix buttons only for the selected machine's current blocked reason and clears them when the machine runs or the selection changes. Views read the reason through the existing application contract; no economy rules in the view.
  - **Tests**: Play Mode regression tests for both observed cases (running machine shows no blocked guidance; switching selection from a blocked to a running machine clears it), at the supported screen classes.
- [X] T366 [US7] Gameplay Developer (Test/QA lens; Medium), after T344, T361 and T364, batch-B Play-Tester DevTools defects F-B2 and F-B4 (owner decision 2026-10-22, #408) [U]:
  - **Overlay input (F-B2)**: the DevTools time-scale overlay consumes pointer events on its controls and marker, so a click on ×1…Max never reaches the slot beneath. Keep the T344 placement below shown notices; avoid a position jump under the pointer where practical (T344 UI/UX note).
  - **Header rows (F-B4)**: the session log appends a further `header` record only when the time-scale factor really changes (a new selected step), not on every jitter of the effective factor in Max (308 of 706 lines in the batch-B session). The record still captures every deliberate change.
  - **Recipe note (F-B5)**: no code. After an accelerated session the game timeline runs ahead of the device clock, so offline return is tested on a never-accelerated save (`quickstart.md` §6).
  - **Tests**: the define-gated `Assets/IdleFactory/Tests/DevTools/` assembly covers the pointer consumption and the header rule; DevTools never reach release builds.
- [X] T345 [US7] Game Systems/Economy (author) with Game Designer/Product Owner review, after T341 and T344, plan step 16 [U]:
  - **Tuning record**: write `reviews/balance-trace-r3.md` with the objective and order, a closed-form fluid pre-solve, the threshold → IP → 4.0× circularity, a Seed Capital replacement, and the re-derivation of the `contracts-off` and `returns` bounds (tolerances only, D-A).
  - **Content revision 3**: tune `contentRevision` 3 in both shipped copies (ratios, cooldowns, build prices, manual-mining yield and cap, threshold growth, milestones, Industry decay, ladder, recipes or prices for `chain.dominance`), with all revision ≥ 3 rules passing.
  - **Fixture**: regenerate `W005-L`, recording both hashes.
  - **Timed-duration cap (owner decision 2026-10-21, #410)**: add a content-validator cap `cooldownMs` and bonus `windowMs` ≤ `MaxTimedDurationMs` (7 days), like the other timed durations, with validator tests. It makes the Save V3 future-instant bound (departure + cooldown or window + 7 days) tight, so a long cooldown that is later shortened cannot make a legitimate save refused.
  - **Drift-class review**: confirm that the r3 retune produces no drift class other than removal or objective change (bonus to standing with a leftover record, presence of `DurationMs` changed), which still load as `checkpoint.invalid`; otherwise raise a follow-up.
  - **Runs**: full Play Mode, the Windows player and `BalanceFull`.
- [X] T346 [US7] Play-Tester (read-only; Medium, sonnet) for gameplay batch C (after T345), owner reviews:
  - **Strategy runs**: on revision 3.
  - **Long session**: an accelerated development-player session to the second prestige.
  - **Report**: `reviews/playtest-agent-batch-c.md`.

---

## Phase 12: Player and device evidence

- [X] T347 Test/QA (independent), after T345, plan step 17a [U]: full Edit Mode and Play Mode suites with 0 skips and a console check. Record in `reviews/full-suite-evidence.md`.
- [X] T348 Gameplay Developer (Test/QA lens), after T345, plan step 17b [U]: `[Category("StandalonePlayer")]` tests in `Assets/IdleFactory/Tests/PlayMode/`:
  - `EconomyJourneyPlayerTests`: mine by hand → first Miner → sell → contract with auto-deliver → prestige at the rising threshold → next-run build price → first Miner again by mining, shape assertions only;
  - a V2→V3 real-store round trip;
  - `W005-L` and V3 fixture SHA-256 parity;
  - log write-failure isolation through the test composition seam (QA-13).

  Run `scripts/test-unity.ps1 -StandaloneWindowsPlayer`.
- [X] T349 Performance Engineer, after T345 and T348, plan step 17c [U]:
  - **W005-L return**: the 8 h return on the Windows player on a quiet host, median of run medians, spread ≤ 10 %.
  - **PF-10 (W003-B with auto-deliver)**: evaluate the 10 % guard against the re-baselined reference of 222.28 ms (T327, `6fc283d`; owner decision 2026-10-21, #414), not against 199.61 ms or 201.12 ms.
  - **Frame time**: Windows frame time with UI over 600 frames at `W005-L` (average and p95).
  - **Acceleration**: in a separate development-player run built with `IDLEFACTORY_DEV`, the acceleration p95 at ×600 and Max on W003-B with auto-deliver, plus frame time during acceleration. The emulator is evidence only.
  - **Platform**: IL2CPP is a recorded open item.
  - **Record**: update `reviews/w005l-measurement.md`. A miss opens a Performance review task with options.
- [X] T350 DevOps/Operations, after T339 and T345, plan step 17d [U]: build a Windows non-development build and an Android release APK (`BuildAndroidArm64Release`), then run `scripts/check-release-build.ps1` on both. Record in `reviews/release-scan.md`. Repeat once per later batch.
- [X] T351 Test/QA (independent), after T345 and T339, plan step 17e [U][E]: extend `scripts/android-emulator-journey.ps1` with F005 steps (sell, auto-deliver toggle, session-log export with the path shown, `adb pull` of the export) and the `W005-L` return (≤ 2 s) at 60 fps. Call it through `-Command "& ./scripts/android-emulator-journey.ps1 ..."`. Record as emulator-only evidence.

---

## Phase 13: Independent reviews, human gates and final acceptance

- [X] T352 UI/UX Reviewer (independent of T319, T324, T330, T333, T340 and T359 authors), after T340, T344, T365 and T366: review every F005 control against `contracts/ui-surfaces-f005.md` and the QA-12 table at every screen class, at 100 % and 200 % text, and with touch and pointer. Record in `reviews/ui-ux-review.md`.
- [X] T367 UI/UX Developer (UI/UX Reviewer, Test/QA and Game Designer/Product Owner lenses; Medium), after T352, before T353 and T355, T352 review findings (owner decision 2026-10-27 on #417: Q8 yes, Q1–Q7 as the reviewer recommended; evidence `reviews/ui-ux-review.md` §3 and §6) [U]:
  - **Industry pointer (R-01, Q1)**: a line and a "Spend N IP in Industry" button on the Prestige panel and in the post-sale banner that open Research scrolled to Industry, plus an IP indicator on the Research tab while IP is unspent.
  - **Offers (R-06, R-07)**: sort acceptable offers first, then cooling down, then ended, used or locked (stable inside a group, no layout jump on the once-per-second tick); a neutral icon for cooldown and used, the warning icon only for a real rejection; one state label per offer derived from the offer's own state, with "finish or abandon the active contract first" as a separate line.
  - **Titles and Item copy (R-08, Q5)**: distinct view-side titles for standing, bonus and production-objective offers (no content or `contentRevision` change); "Item reward limit reached this run" when an Item reward is withheld, until `perRunCap` (#387) is decided.
  - **Copy (R-10, R-24, R-31, R-02/Q2)**: locked-Contracts text groups conditions of one kind ("…produce Iron Plate, Gear or Iron Bar"); Industry cards state the next level's own gain instead of "per level"; the idle welcome-back headline and efficiency line tell one story; "$" for every amount and "Cash" only as the balance label.
  - **Machine panel (R-20, R-23)**: when guidance rows are cleared or a fix button is used, focus moves to a stable visible sibling; the "Build a Miner" hint appears only when no producer of that resource is owned (the Architect lens decides view filter or Core action condition).
  - **Research (R-25, R-27)**: one message and one icon after a rejected purchase; remove the unsupported `:last-child` rule so the console stays clean.
  - **Contract wording (R-04)**: amend `contracts/ui-surfaces-f005.md` so the Sell controls sit below the row.
  - **Tests (Q4)**: Play Mode cases for every fix above, plus one touch and one pointer case each for contracts, prestige, session log, Industry and Overdrive, at the supported screen classes and 100 %/200 % text.
  - **Re-review**: an independent UI/UX reviewer (not the author) appends a re-review of R-01, R-02, R-04, R-06–R-08, R-10, R-20, R-23–R-25, R-27 and R-31 to `reviews/ui-ux-review.md`.
- [X] T353 Owner (human gate), after T345, T346 and T367:
  - **Playtest**: a named human playtest on the shipped build that reaches the first prestige and a second run.
  - **Record**: write `reviews/playtest-<name>-<date>.md` with the exported session log, the human-to-trace ratio at the timeline samples, and the 1–5 answer to "Did both selling and contracts matter to your income?" (SC-005, #358).
- [X] T354 Owner (human gate), after T353: approve or reject `contentRevision` 3 in `reviews/content-approval-r3.md` (FR-027).
- [X] T355 Parent with independent reviewers (Game Designer/Product Owner, Software Architect, Test/QA, Game Systems/Economy, UI/UX, Performance, Security/Privacy, DevOps), after T347–T354 and T367: final verdicts on the exact accepted commit, recorded in `reviews/feature-review.md` and `reviews/final-acceptance.md`. Review waves respect `max_parallel: 3`.
- [X] T356 Parent, after T355: closeout hooks and convergence.
  - Mirror the checkboxes with `scripts/maqa-sync-task-checkboxes.ps1 -TasksFile specs/005-economy-contracts/tasks.md`.
  - Run `$speckit-wiki-ingest specs/005-economy-contracts`, `$speckit-learn-review`, `$speckit-wiki-lint` and `scripts/check-after-implement-hooks.ps1 -FeatureDir specs/005-economy-contracts`, then `$speckit-converge`.
  - Run `scripts/maqa-stale-worktrees.ps1`.

---

## QA-11: Save V3 fixture-to-assertion table (T334, T335)

| Fixture | Assertion |
|---|---|
| `v2/*` (all existing) | Loads. V3 defaults (auto-deliver false, produced 0, no cooldowns, no bonus, `runCompletionTotal` = sum of counts). Equivalent state. |
| `v3-autodeliver-true` | The flag restored on a delivery contract. |
| `v3-autodeliver-on-produce` | Rejected (`checkpoint.invalid`). |
| `v3-produced-over-required` | Rejected. |
| `v3-bool-wrong-type` | Rejected. |
| `v3-cooldown-duplicate-offer` | Rejected. |
| `v3-cooldown-past` | Loads as ready. |
| `v3-bonus-missing-windowend` (triggered and consumed) | Rejected. |
| `v3-bonus-lapsed-triggered` | Loads, and never re-triggers after reload. |
| `v3-unknown-member` | Rejected. |
| `v3-drift-removed-offer` | Contract-scoped entries dropped, `contract.dropped` reported, `runCompletionTotal` kept, no Item re-grant. |
| `v3-drift-objective-changed` | Active contract dropped. |
| `v3-drift-unknown-machine` | Rejected (`checkpoint.content`). |
| `v4-version` | Refused (`checkpoint.version`), with no write and no V1 import. |
| V2 load followed by an injected failure of the first V3 write | The V2 generation stays readable. |

## QA-12: Control × state × screen-class table (T319, T324, T330, T333, T340, T359)

Every row runs at every `ShellScreens.All()` entry, at 100 % and 200 % text, with worst-case content (6-digit values, longest resource name, longest reason). Each row is checked with touch and pointer exact-value inspection, accessible-name assertions per state, and focus on the first, middle and last control.

| Control | States to assert |
|---|---|
| Ore vein | click, tap, keyboard; hold at the cap; progress in tenths; excess ignored with a max-rate cue; pooled pulse and static fallback; store full and recovery; value-limit and lifecycle copy; disabled during a dialog; hidden under a sheet; hold ended by release, leaving, a second pointer, rotation, focus loss or a dialog with no extra Ore; no peek on a touch hold |
| Sell selector and button | normal; nothing to sell; locked; "Need N more"; stale tap re-render with focus kept; All confirmation (shortfall, then rotation cancel); success notice; each rejection copy (`resource.locked`, `sell.quantity-invalid`, `stock.insufficient`, `value.limit`) |
| Build price | affordable; "Need $X more"; capped; `build.price.changed` recovery; Remove confirmation with the rebuild price |
| Auto-deliver toggle | off and on; hidden for production with a hint; Off after re-accept; auto-delivered notice; short-stock expiry line; blocked state |
| Offer cooldown and bonus | countdown tick; bonus window end transition; Accept rejection copy (`contract.cooldown`, `contract.bonus.unavailable`, `contract.capacity`) |
| Production progress | progress; completion |
| Contracts badge | label "(n)" or "!" ; compact `tab-contracts-badge` "•"; cleared after a sale or auto-sell |
| Goal line | next goal; reached notice; Factory Sale available; threshold-only (no milestones); all reached |
| Prestige threshold | current and next; at `maxIndex` (same value) |
| Session-log section | on and off; export path; nothing to export; export failure with retry; clear confirmation with rotation cancel; last export line |
| DevTools overlay (dev assembly tests) | each factor; marker text and name; no overlap at 200 % |

---

## Dependencies and execution order

- **Phase 1**: T300 runs in parallel with Phase 2 and gates T316, T325 and T326 (through T360). T301 gates T341.
- **Phase 2**:
  - T302 → T303 → T304.
  - After T304, these can be authored in parallel: T305, T306, T307, T308 (→ T309), T310, T311 and T312.
  - Then T312 → T313 → T314 → T315.
- **Option 1**: T360 after T315 and T300, before T316 and T326.
- **User stories after T315**:
  - US1: T316 (after T360) → T317 → T318 and T319.
  - US10: T358 (after T317 and T323) → T359.
  - US2: T320 (after T317) → T321 and T322.
  - US4: T323 (∥) → T324.
  - US3: T325 (after T321, T322 and T300) → T326 (after T360) → T327 → T328, T329 and T330.
  - US5/US6: T331 (∥) → T332 → T333.
  - Save V3: T334 (after T327) → T335 → T336.
  - US9: T337 (after T306, T308, T325 and T334).
  - US8: T338 (after T307 and T335) → T339 and T340.
- **US7**: T364 after T338 and T337, before T343. T341 after T301, T311, T317, T327, T323, T331 and T310. T342 after batch A. T361 after T342, T308 and T359; T362 after T342, T319 and T333; T363 after T342, T321 and T322. T343 after batch B and T361–T364, T344 after the UI tasks, T345 after T341 and T344, T346 after T345. T365 after T344 and T362; T366 after T344, T361 and T364; both before T352 (owner decisions 2026-10-22 on the batch-B Play-Tester report, #408).
- **Evidence and gates**: T347–T351 after T345. T352–T356 last. T367 after T352 and before T353 and T355 (owner decision 2026-10-27 on the T352 review, #417).
- **Shared files (analysis F2)**: `docs/contracts/run-reason-codes.md` and `Assets/IdleFactory/Unity/Content/RunContentValidator.cs` are edited by T316, T317, T320, T321, T323, T331, T335 and T358. Tasks marked ∥ may be authored in parallel, but their merges into these two files go one after another in MAQA merge order. Each later task rebases onto the earlier merge.

## Batches (for MAQA)

| Batch | Tasks | Notes |
|---|---|---|---|
| 0 | T300, T301, T302–T304 | refactor serial; T300 and T301 in parallel |
| 1 | T305–T315, T357 | follow-ups, tooling, strategy framework, content v3. Play-Tester report T357. |
| A | T360, T316–T324, T358, T359 | US1, US10, US2, US4. The Play-Tester report T342 is due before the batch closes. |
| B | T325–T340, T361–T364 | US3, US5, US6, Save V3, US9, US8, plus the batch-A Play-Tester follow-ups T361–T363 (owner decisions 2026-10-15) and the session-log raise sites T364 (owner decision 2026-10-21, #403). Play-Tester report T343. |
| C | T341, T344–T346, T365, T366 | balance r3 and tuning, plus the batch-B Play-Tester follow-ups T365 and T366 (owner decisions 2026-10-22, #408). Play-Tester report T346. |
| D | T347–T356, T367 | evidence, reviews, gates, closeout, plus the T352 review fixes T367 (owner decision 2026-10-27, #417) |

Each batch ends with the after_implement hooks, the hook gate and the stale-worktree report. The parent then proposes a fresh session (AGENTS.md).

## Parallel Execution

### Parallel Opportunities

Work-unit ordering in the ledger controls orchestration, and `[P]` markers are local hints only.

- **Parallel candidates**: units whose prerequisites are all accepted and whose paths are disjoint. Examples:
  - WU06 to WU12 after WU05;
  - WU23 (build pricing) and WU36 (threshold) once WU16 is accepted;
  - the UI units for Sell, Build price, Ore vein, Contracts, Goals and Session log, on separate panel paths.
- **Limits**: at most three agents run at a time (`max_parallel: 3`). Unity verification is serialized by the `unity` lease. Merges into the two shared files listed in the parent rules go one after another.

## Subagent Handoff Examples

**Test-oriented example (WU33, T328)**:

> Role: Gameplay Developer with Architect and Test/QA lenses. Medium tier, sonnet. Issue #<n>, worktree `maqa/issue-<n>-...`.
>
> - Task: T328 verbatim.
> - Paths: `Assets/IdleFactory/Tests/EditMode/Run/AutoDeliverEquivalenceTests.cs` only.
> - Sources: research F5-R05, F5-R17, F5-R18; contracts/run-application-f005.md › Determinism obligations; plan › Event order.
> - Constraints: tests only, no production edits. Each case must fail against a deliberately broken order before it is accepted.
> - Validation: `scripts/test-unity.ps1 -TestFile Assets/IdleFactory/Tests/EditMode/Run/AutoDeliverEquivalenceTests.cs` under the `unity` lease.
> - Response: files changed, tests run with results, assumptions, verdict.

**Implementation-oriented example (WU25, T358)**:

> Role: Gameplay Developer with Game Systems, Security and Test/QA lenses. Medium tier, sonnet. Issue #<n>.
>
> - Task: T358 verbatim.
> - Allowed paths: `Assets/IdleFactory/Core/Run/Application/RunCommands.Mining.cs`, the `RunState` mining counter, the effect vocabulary, `Assets/IdleFactory/Unity/Content/RunContent*.cs` (the `manualMining` member and rules), `docs/contracts/run-reason-codes.md`, and `Assets/IdleFactory/Tests/EditMode/Run/ManualMiningTests.cs`.
> - Forbidden: UI, scenes, saves.
> - Sources: spec US10, FR-040 to FR-044, SC-011; research F5-R19, F5-R20; contracts/gameplay-content-v3.md; data-model.md › Run state.
> - Validation: the targeted test file, the full Edit Mode suite, and `ReasonCodeContractTests`.
> - Response: as in the packet.

## Implementation Strategy

- **MVP**: Phases 1–2 plus US1 (manual selling) and US10 (manual mining).
- **Then**: US2 and US4 (contract headroom and price scaling) close the core economy problem.
- **Then**: US3, US5, US6 and US9 complete the loop.
- **Last**: US8 and US7 deliver the measurement and tuning that revision 3 and the human playtest need.

### Context-Orchestrated Subagent Strategy

- Each batch in the batch plan is executed as a sequence of ledger units through MAQA: sync, coordinator, worker, QA and merge. Every unit gets a fresh worker with the handoff packet.
- The parent verifies each unit before its dependents start. It runs the Windows player suite once per batch and the after_implement hooks, the hook gate and the stale-worktree report at the end of each batch. It then proposes a fresh session.
