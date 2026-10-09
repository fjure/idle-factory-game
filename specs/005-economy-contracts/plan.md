# Implementation Plan: Economy and Contracts

**Branch**: `feat/005-economy-contracts` | **Date**: 2026-10-10 | **Spec**: [spec.md](spec.md)

**Input**:
- the clarified specification (owner decisions D-A…D-I and the clarify session of 2026-10-10);
- [reviews/discovery.md](reviews/discovery.md) (both review waves);
- the F003 plan and contracts;
- the constitution;
- Phase 0 research in [research.md](research.md) (F5-R01…F5-R16).

## Summary

F005 changes the F003 run model in place. It adds no new model.

1. A behaviour-preserving refactor (#356) splits `RunPersistence` and `RunSnapshots` and makes the command-path `RunState` mutators internal.
2. Development-only time acceleration and the measurement tools follow, so the mandatory Play-Tester can work from the first gameplay batch.
3. Gameplay then adds:
   - one shared sale price with a manual `Sell` command (F5-R04);
   - ratio-based contracts with cooldowns, bonus offers, production objectives and one Item ladder;
   - auto-delivery evaluated inside `RunSimulator` at completion instants (F5-R05);
   - build prices that scale with owned count and prestige count (F5-R06);
   - a rising prestige threshold with display-only milestones (F5-R07);
   - Industry upgrades with diminishing returns (F5-R08).

Content moves to schema v3 (F5-R03) and saves to Save V3, which still reads V2 (F5-R09). A wall-clock gap after OS sleep becomes a standard offline return (F5-R10). A local session log records allowlisted play data through a Core port and a failure-isolated Unity adapter (F5-R12). Reference policy r2 and five strategies drive a balance trace over runs 1–5 (F5-R13). Follow-ups #353, #354 and #355 are scheduled work units.

## Technical Context

**Language/Version**: C# supported by Unity `6000.6.0f1` (.NET Standard 2.1 profile). No unsafe code and no `double` in authoritative rules.

**Primary Dependencies**: Only existing packages: Newtonsoft JSON `3.2.2`, Input System `1.20.0`, UI Toolkit, Unity Test Framework `1.8.0`. Android share export uses `androidx.core` `FileProvider` through Unity's Gradle template. That is a build-configuration change (F5-R12) whose final cost the plan review decides. No UPM package is added.

**Storage**:
- content `Assets/IdleFactory/Content/Iron/iron-factory-content.json` (schema v3);
- Save V3 under `persistentDataPath/IdleFactory/Checkpoints/v2/` (unchanged slot names);
- session log under `persistentDataPath/IdleFactory/SessionLog/` and exports under `.../Exports/`;
- the log on/off setting in `PlayerPrefs`.

**Testing**:
- **Edit Mode NUnit**: the public API snapshot, the reason-code pin, pricing, sell, contracts (rewards, cooldown, bonus, produce, ladder), auto-deliver equivalence, build pricing, threshold, goals, Industry, content v3 rule codes, Save V3 matrix and fixtures, live gap, the session-log schema, and the balance trace (fast and full).
- **Play Mode**: the new panel states at every screen class and 100/200 % text, focus, the desktop lifecycle (#354), and log export.
- **StandalonePlayer (Windows)**: the F005 journey, `W005-L` timings and acceleration timing.
- **Android emulator**: the F005 steps and the `W005-L` return.

**Target Platform**: Windows editor and Standalone player (required evidence); Android ARM64 development player on the API 36 x86_64 AVD (emulator-only). iOS is deferred (#39).

**Project Type**: single Unity 2D game repository.

**Performance Goals** (F5-R14):
- `W005-L` 8 h return ≤ 500 ms on the Windows player and ≤ 2 s on the emulator;
- 60 fps on both;
- 0 `GC.Alloc` samples per warmed `Advance`, also with logging on;
- acceleration p95 ≤ 1 ms per host frame at S_max = 10 s per frame on Windows.

W003-B stays a recorded bound (D-H).

**Constraints**:
- Core stays engine-free.
- Checked integers ≤ 10^18.
- Nothing is published partially.
- No reflection deserialization (IL2CPP).
- No network.
- Dev tools never compile into release builds.
- The session log never alters simulation, save or timing.

**Scale/Scope**:
- 4 resources and 4 machine types, ≤ 12 machines;
- content offers: 3 standing delivery, ≥ 1 production, ≥ 1 bonus;
- milestones ≤ 8;
- prestige `maxIndex` ≤ 64;
- 5 strategies × 5 runs × 5 offsets in the full trace.

No NEEDS CLARIFICATION remains.

## Constitution Check

*GATE: passed before Phase 0 and rechecked after Phase 1.*

| Principle | Evidence | Result |
|---|---|---|
| I. Correct observable behavior | Every FR maps to an algorithm in research and a named test. Edge cases are in the spec and the test matrix. Defaults adopted from discovery are listed for owner override. | PASS |
| II. Domain architecture, explicit state | New rules sit in Core (`SalePricing`, `BuildPricing`, `PrestigeThreshold`, `ContractRewards`). Command mutators become internal (F5-R01). New state has one owner each (data-model). The log is a port with an adapter. | PASS |
| III. Deterministic, data-driven | Integer ppm math, one floor or ceiling per formula, auto-delivery at completion instants with partition tests, all tuning in content v3 with rule codes. | PASS |
| IV. Tests protect behavior | Test strategy and traceability below. The #356 gate is a public-API diff plus unchanged suites. #355 pins reason codes against the contracts. | PASS |
| V. Simplicity | No new package, one new assembly (DevTools, justified below), no event bus (a single sink port). Smart auto-sell, auto-accept and queues are excluded. | PASS (1 complexity entry) |
| VI. Measured performance | #353 is measured before changes, `W005-L` is a gate, and acceleration and log have declared budgets (F5-R02, F5-R14). | PASS |
| VII. Human authority | Owner decisions D-A…D-I and five clarify answers are recorded. `contentRevision` 3 needs owner approval after the human playtest. Play-Tester reports go to the owner, who has a veto. | PASS |
| VIII. Traceability | FR/SC → design → contract → evidence table below. | PASS |
| IX. Independent review | Review waves and role disposition below. The Play-Tester is mandatory per affected batch. | PASS |
| X. Safe delivery, durable knowledge | Save V3 is fail-closed and reads V2, the release-build scan, the Android backup gate, and a post-plan wiki ingest. | PASS |

Post-design recheck: no new violations.

## Owner decisions

All are recorded in spec › Clarifications (sessions 2026-10-10).

| ID | Decision | Implemented by |
|---|---|---|
| D-A | One feature. The offline-value redesign is out, and 80 % and 8 h stay unchanged. | Scope guard; F5-R13 bounds are tolerances only |
| D-B | #356 first, then tooling (time scale, policies), then gameplay | Work breakdown order |
| D-C | Rising threshold, canonical ECO-3 (FR-021), Industry diminishing with ceiling ≥ 4.0× IP of runs 1–4 | F5-R07, F5-R08, trace gate |
| D-D | Contract reward ratio on base price with sale modifiers; standing ≤ 1.5, bonus ≤ 2.0 | F5-R03, F5-R05 |
| D-E | Chain dominance per slot within the integer format, enforced by a static gate | `chain.dominance` rule |
| D-F | Local session log; smart auto-sell out; follow-ups #353–#357 in scope; F004 owns styling | F5-R12, work breakdown |
| D-G | Save V3 reads V2 | F5-R09 |
| D-H | W003-B is a bound; `W005-L` is the gate; #353 measured on `a41c53f` first | F5-R02, F5-R14 |
| D-I | Accelerated sessions are marked in the log only | F5-R11, F5-R12 |
| Clarify | Bonus once per run on a milestone with a window; build price by prestige count; cooldown on every ending; production counts completions without removing stock; log can be switched off | F5-R03, F5-R05, F5-R06, F5-R12 |

## Design

### Components

| Component | Assembly / namespace | Responsibility | Requirements |
|---|---|---|---|
| `RunPersistence` (split), `SafeRunStore`, `OfflineReturn`, `RunSnapshots.*` (split) | Core `IdleFactory.Run.Application` | #356 structure. Adds `RecordDepartureAt` and the minimum autosave interval. | FR-033, FR-036, F5-R01, F5-R10 |
| `SalePricing` | Core `IdleFactory.Run.Application` | Single sale formula for auto-sell, `Sell` and valuation | FR-001–FR-003, F5-R04 |
| `RunCommands.Sell` | Core Application | `Sell`, `SellAll` | FR-001, FR-003 |
| `ContractRewards`, `ContractBoard` (cooldowns, bonus, produced, auto-deliver) | Core Application / Domain | Shared grant arithmetic, offer lifecycle | FR-004–FR-012 |
| `RunSimulator` (changed) | Core `IdleFactory.Run.Simulation` | Per-instant order: completions (+ produce count) → auto-delivery → auto-sell with reserve → retry; new boundary events | FR-011, FR-012, F5-R05 |
| `BuildPricing` | Core Application | Price curve, cap, discounts; `Build(expectedPrice)` | FR-013–FR-016, F5-R06 |
| `PrestigeThreshold`, `GoalsSnapshot` | Core Application | Threshold by `PrestigeCount`, milestones | FR-018–FR-020, F5-R07 |
| Industry value tables | Core `IdleFactory.Run.Content` | Diminishing values, practical ceiling | FR-021, F5-R08 |
| `IRunEventSink`, event structs | Core Application | Allowlisted log events (no simulator calls) | FR-028–FR-030, F5-R12 |
| Content v3 reader, validator, builder; dispatcher | Unity `IdleFactory.Unity.Content` | Schema v3 and rule codes | F5-R03 |
| `SaveV3` DTOs, mapper (reads V2 and V3) | Unity `IdleFactory.Unity.Persistence` | Save V3 contract | FR-034, F5-R09 |
| `FactoryRuntimeHost` (changed) | Unity Bootstrap | UTC gap detection, `ITimeScaleSource` seam, S_max clamp | FR-026, FR-032, F5-R10, F5-R11 |
| `SessionLogWriter`, export, setting | Unity `IdleFactory.Unity.Persistence` (log) and UI | Ring buffer, segments, CRC, export, clear, toggle | FR-028–FR-031 |
| `IdleFactory.DevTools` (new asmdef, `IDLEFACTORY_DEV`) | DevTools | Acceleration overlay, marker, log flag | FR-026, F5-R11 |
| Panel changes (Market sell, contracts, build sheet, goals, prestige, settings) | Unity UI | [ui-surfaces-f005.md](contracts/ui-surfaces-f005.md) | FR-038, FR-039 |
| `ReferencePolicy` r2, `IStrategyPolicy` set, `BalanceTraceRunner` r3 | Edit Mode tests `Balance/` | Trace, ensemble, gates | FR-022–FR-025, SC-002, SC-004 |
| `scripts/check-release-build.ps1`, `test-unity.ps1 -Category`, `PlatformBuildCommands` dev variants | scripts, Editor | Release absence, Android backup gate, balance category | FR-026, FR-031 |

### Data flow changes

- **Start-up**: content v3 → `RunCatalog` → the Save V3/V2 reader (content drift drops unknown offers with `contract.dropped`) → session.
- **Frame**:
  1. The UTC delta is checked.
  2. A gap above the threshold leads to `RecordDepartureAt` and then `ResumeAndReconcile`.
  3. Otherwise the scaled delta, clamped to S_max, goes to `Advance`.
- **Commands and transitions**: they raise `IRunEventSink` events. `SessionLogWriter` buffers them and flushes at lifecycle points or every 30 s or more.
- **Presenters**: they read `EconomySnapshot`, `ContractsSnapshot`, `GoalsSnapshot` and the factory snapshot (with the build price cached per revision).

### Event order and determinism

`RunSimulator.ProcessTimestamp`:

1. expiries (deadline, effects, Overdrive, bonus window);
2. priority completions, with production counting;
3. auto-delivery;
4. auto-sell with the reserve;
5. the OD-2 retry, repeating 2–4;
6. the run predicate.

The F003 equivalence suite gains these cases:

- (l) auto-delivery in single against partitioned advances, live and offline, including the stock crossing mid-segment and the deadline instant;
- (m) a production objective across partitions;
- (n) the reserve with auto-sell on the same resource;
- (o) cooldown and bonus-window boundaries;
- (p) a large live gap equal to a quit and restart;
- (q) a sale and valuation conservation invariant per step.

### Persistence, offline, prestige

These follow [contracts/save-v3.md](contracts/save-v3.md). The fault-injection seams of F003 apply to V3 writes and to the V2 → V3 first write. The prestige candidate carries `PrestigeCount + 1`, and a retry test proves the index advances exactly once.

### UI and interaction

The controls are listed in [contracts/ui-surfaces-f005.md](contracts/ui-surfaces-f005.md). They are functional and follow `run-panel-conventions`. The plan review wave (UI/UX) decides the Session log placement.

### Art and technical art

None. F005 adds no assets, and its badges and markers are text. Art Direction and Technical Art are omitted, and F004 owns visuals.

### Performance

These follow F5-R02 and F5-R14.

| Workload | Use |
|---|---|
| `W005-L` | Gate. A Save V3 fixture near the run-3 threshold from the policy-r2 trace, with auto-deliver active. Its hash is recorded. |
| `W003-B` | Bound. Measured on `a41c53f`, after #356 and after the simulator change, with and without auto-deliver. |

Metrics and budgets are listed in Technical Context. A miss triggers a Performance review with options. There is no semantic change without an owner decision.

### Test strategy

| Level | Scope | Named suites (new) |
|---|---|---|
| Edit Mode | API snapshot; reason pin; `SalePricing`, `Sell`; contracts (rewards, cooldown, bonus, produce, ladder); auto-deliver equivalence (l)–(q); `BuildPricing`; threshold and IP index retry; goals; Industry tables; content v3 rule codes; Save V3 matrix and V2 fixtures; live gap; log schema and isolation; balance fast gate | `PublicApiSnapshotTests`, `ReasonCodeContractTests`, `SalePricingTests`, `RunSellCommandTests`, `ContractRewardsTests`, `ContractCooldownTests`, `BonusOfferTests`, `ProduceObjectiveTests`, `AutoDeliverEquivalenceTests`, `BuildPricingTests`, `PrestigeThresholdTests`, `GoalsSnapshotTests`, `IndustryDiminishingTests`, `RunContentV3*Tests`, `SaveV3*Tests`, `LiveGapTests`, `SessionLogSchemaTests`, `BalanceTraceTests` |
| Edit Mode (`BalanceFull`) | 5 strategies × runs 1–5 × 5 offsets; FR-024 gates; SC-002/SC-004; #357 rows | `BalanceTraceFullTests` |
| Play Mode | Every new control and state at every screen class at 100/200 %; focus; Contracts badge; desktop lifecycle with #354 (failing write, quit then focus loss, minimum interval); log export, toggle and clear | `MarketSellPanelTests`, `ContractsAutoDeliverPanelTests`, `GoalsLinePanelTests`, `BuildPricePanelTests`, `SessionLogPanelTests`, `RunDesktopLifecycleTests` (+) |
| StandalonePlayer (Windows) | F005 journey (sell → contract with auto-deliver → prestige at the rising threshold → next-run price); `W005-L` timings; acceleration p95 at S_max (development player); log write failure isolation | `EconomyJourneyPlayerTests`, `W005PlayerTimingTests`, `TimeScalePlayerTests` |
| Release build scan | No DevTools in a non-development build; Android backup exclusion | `scripts/check-release-build.ps1` |
| Android emulator | Sell, auto-deliver, log export; `W005-L` return; 60 fps | `scripts/android-emulator-journey.ps1` with F005 steps |
| Agent and human | Play-Tester report per affected batch; named human playtest to the second run; owner content approval | `reviews/playtest-agent-*.md`, `reviews/playtest-*.md`, `reviews/content-approval-r3.md` |

### Requirement traceability

| Requirement | Design / evidence |
|---|---|
| FR-001–FR-003, SC-003 | `SalePricing`, `Sell`; `RunSellCommandTests`; equivalence (q); sell panel |
| FR-004–FR-009, SC-001 | Content v3 ratio rules; `ContractRewards`; cooldown, bonus, produce, ladder suites |
| FR-007a | Prestige reset tests for contract state |
| FR-010–FR-012 | Simulator order; `AutoDeliverEquivalenceTests` (l)–(n); Save V3 flag |
| FR-013–FR-017 | `BuildPricing`; `build.*` and `chain.dominance` rules; ECO-7 test |
| FR-018–FR-021, SC-004 | `PrestigeThreshold`, `GoalsSnapshot`, Industry tables; trace gates |
| FR-022–FR-025, FR-027, SC-002, SC-005 | Policy r2, strategies, `BalanceTraceFullTests`; human playtest; content approval |
| FR-026, SC-007 | DevTools assembly, S_max clamp, release scan, `TimeScalePlayerTests` |
| FR-028–FR-031, SC-008 | `IRunEventSink`, `SessionLogWriter`, schema and isolation tests, export on Windows and emulator, backup gate |
| FR-032, FR-033 | `RecordDepartureAt`, `LiveGapTests` (p), #354 tests |
| FR-034, SC-006 | Save V3 contract and fixtures |
| FR-035 | `ReasonCodeContractTests` |
| FR-036 | `PublicApiSnapshotTests` and unchanged suites |
| FR-037 | `reviews/w003b-measurement.md` before the selling work unit |
| FR-038, FR-039, SC-009 | UI contract; panel, layout and focus tests; UI/UX review |
| SC-010 | Play-Tester reports reviewed by the owner per affected batch |

### Review waves and role disposition

- **Plan review wave** (after this plan, `max_parallel: 3`):
  - wave A: Software Architect, Test/QA, Gameplay Developer;
  - wave B: Game Systems/Economy (content v3, formulas, policy r2), UI/UX (UI contract and Session log placement), Performance (F5-R02, F5-R14);
  - wave C: Security/Privacy (Save V3, log, export) and DevOps/Operations (DevTools define, build variants, release scan, Android manifest and backup rules, `test-unity.ps1 -Category`).

  The record goes to `reviews/plan-reviews.md`, and its dispositions are applied to these artifacts before `$speckit-tasks`.
- **Mandatory lenses**:
  - Product Owner, Architect, Test/QA;
  - Game Systems/Economy;
  - UI/UX (player-visible);
  - Performance (hot path, `W005-L`);
  - Security/Privacy (persisted data, log);
  - DevOps (build variants, release gate, manifest);
  - Play-Tester (per affected batch, owner veto).
- **Omitted**:
  - Art Direction and Technical Art: no assets, F004 owns visuals.
  - Sound and Composer: not in scope.
  - Online Services: no remote boundary.
  - 3D/Render: none.

### Proposed work breakdown (input to `$speckit-tasks`)

Markers: ∥ = disjoint paths, can run in parallel. **[U]** = needs the `unity` lease (plus `emulator` when the target may be Android). **[E]** = needs the emulator.

0. **#353 Option 0**: W003-B on `a41c53f`, Windows player and emulator, phase attribution, record `reviews/w003b-measurement.md` [U][E]. Performance Engineer.
1. **#356 refactor**: file split, internal mutators, `InternalsVisibleTo`, `PublicApiSnapshotTests` [U] (after 0).
2. ∥ **#355 reason-code pin** and the contract update [U] (after 1). ∥ **#354** failing-write and quit-then-focus-lost tests plus the minimum autosave interval [U] (after 1).
3. **Tooling**:
   - 3a: `IdleFactory.DevTools` assembly, `ITimeScaleSource` seam, S_max clamp, marker, build variants, `check-release-build.ps1` (DevTools part) [U];
   - 3b: `test-unity.ps1 -Category` (DevOps; no Unity lease for the script edit, a lease for its verification run).

   Both run after 1.
4. **Content schema v3**: reader, validator (all rule codes), builder, dispatcher; migrate the shipped file to v3 with values equivalent to r2 where the shape allows, so behaviour stays as close as possible until step 15 [U] (after 1).
5. **Sale pricing and `Sell`**: `SalePricing` shared by auto-sell and valuation, `Sell`/`SellAll`, `EconomySnapshot` [U] (after 4; requires the step 0 decision record; applies Option 1 first if step 0 chose it).
6. **Contracts**: ratio rewards, `ContractRewards` extraction, cooldowns, bonus offers, production objective, Item ladder, prestige reset (FR-007a) [U] (after 4 and 5).
7. **Auto-deliver in the simulator**: order, reserve, boundary events, equivalence (l)–(o) [U] (after 6). Then re-measure W003-B with auto-deliver (Performance).
8. ∥ **Build pricing**: `BuildPricing`, `Build(expectedPrice)`, snapshot cache, ECO-7 test [U] (after 4).
9. ∥ **Threshold, goals, Industry**: `PrestigeThreshold`, IP index retry test, `GoalsSnapshot`, diminishing Industry tables [U] (after 4).
10. **Save V3**: DTOs, mapper reading V2/V3, content drift, fixtures, fault injection [U] (after 6 and 7).
11. **Live gap**: `RecordDepartureAt`, host UTC detection, interaction with the time scale, (p) [U] (after 2, 3a and 10).
12. **Session log**:
    - 12a: Core sink and events from commands and transitions [U] (after 5–9);
    - 12b: Unity writer, ring buffer, segments, CRC, export (path), toggle, clear, isolation tests [U] (after 12a and 10);
    - 12c: Android share intent and backup rules plus the manifest check in `check-release-build.ps1` [U][E] (after 12b; can be dropped to path-only by the plan review).
13. **Balance**: policy r2 (Game Systems writes `reviews/balance-policy-r2.md`), strategy set, runner r3, ensemble, FR-024 gates, non-rounding price variant [U] (after 5–9).
14. **UI**:
    - 14a: Market sell [U] (after 5);
    - 14b: contracts (auto-deliver, cooldown, bonus, produce, badge) [U] (after 6 and 7);
    - 14c: build price, goals line, prestige threshold [U] (after 8 and 9);
    - 14d: session log settings and DevTools overlay polish [U] (after 12b and 3a);
    - then an independent UI/UX review of 14a–14d.

    14a–14d can run in parallel on separate panel paths.
15. **Content r3 tuning** (Game Systems): values for ratios, cooldowns, build prices, threshold growth, milestones and Industry decay, iterated on the trace [U] (after 13; gameplay batches before this run on the migrated r2-equivalent values).
16. **Player and device evidence**:
    - 16a: full Edit Mode and Play Mode suites and a console check [U];
    - 16b: StandalonePlayer journey [U];
    - 16c: the `W005-L` fixture, timings and acceleration p95 [U];
    - 16d: release build scan [U];
    - 16e: Android emulator journey, export and `W005-L` [U][E].
17. **Play-Tester and human gates**: a Play-Tester report for every gameplay batch (strategy runs, and player sessions when the flow changed); a named human playtest to the second run with the exported log; owner approval of `contentRevision` 3.
18. **Independent reviews and final acceptance**: Product Owner, Architect, Test/QA, Game Systems, UI/UX, Performance, Security and DevOps records, plus convergence.

## Project Structure

### Documentation (this feature)

```text
specs/005-economy-contracts/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/ (run-application-f005.md, gameplay-content-v3.md, save-v3.md, session-log.md, ui-surfaces-f005.md)
├── checklists/ (requirements.md, requirements-review.md)
├── reviews/ (discovery.md; later plan-reviews, w003b-measurement, balance-policy-r2, balance-trace-r3, playtest-agent-*, playtest-*, content-approval-r3, final-acceptance)
└── tasks.md            # $speckit-tasks
```

### Source code (repository root)

```text
Assets/IdleFactory/Content/Iron/iron-factory-content.json            # schema v3, contentRevision 3
Assets/IdleFactory/Core/Run/Application/RunPersistence*.cs, OfflineReturn.cs, SafeRunStore.cs   # #356 split
Assets/IdleFactory/Core/Run/Application/RunSnapshots.{Factory,Economy,Contracts,Items,Goals}.cs, RunDerivation.cs
Assets/IdleFactory/Core/Run/Application/{SalePricing,BuildPricing,PrestigeThreshold,ContractRewards}.cs
Assets/IdleFactory/Core/Run/Application/RunCommands.Sell.cs, IRunEventSink.cs, RunEvents.cs
Assets/IdleFactory/Core/Run/{Content,Domain,Simulation}/*            # changed definitions, ContractBoard, RunSimulator
Assets/IdleFactory/Unity/Content/Run*V3*.cs                          # v3 reader/validator/builder
Assets/IdleFactory/Unity/Persistence/{SaveV3*,SessionLogWriter,SessionLogExport}.cs
Assets/IdleFactory/Unity/Bootstrap/FactoryRuntimeHost.cs             # gap detection, time-scale seam
Assets/IdleFactory/Unity/UI/*                                        # panel changes
Assets/IdleFactory/DevTools/IdleFactory.DevTools.asmdef + overlay    # new, IDLEFACTORY_DEV only
Assets/IdleFactory/Editor/PlatformBuildCommands.cs                   # dev build variants
Assets/Plugins/Android/{AndroidManifest.xml,res/xml/*}               # 12c: FileProvider and backup rules
Assets/IdleFactory/Tests/EditMode/{Run*,SaveV3*,Balance/,Fixtures/{SaveV3,Balance}}/
Assets/IdleFactory/Tests/PlayMode/*Panel*Tests.cs, *PlayerTests.cs
scripts/check-release-build.ps1 (new), scripts/test-unity.ps1 (+ -Category), scripts/android-emulator-journey.ps1 (+ F005 steps)
```

**Structure Decision**: The four-assembly layout stays, plus one gated `IdleFactory.DevTools` assembly. All rules stay in `IdleFactory.Core`.

## Complexity Tracking

| Violation / cost | Why needed | Simpler alternative rejected because |
|---|---|---|
| A fifth assembly, `IdleFactory.DevTools`, behind a dedicated define | Release builds must provably contain no acceleration (FR-026, Security condition 5). Assembly exclusion can be scanned in the build artifact. | `#if DEVELOPMENT_BUILD` blocks inside `IdleFactory.Unity`: test and profiling players are development builds, and code-level exclusion cannot be verified by a simple artifact scan |
