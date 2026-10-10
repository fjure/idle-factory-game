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
- Task IDs start at T300. F003 ends at T251. The F004 draft (T193–T242) is renumbered before its own sync.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: disjoint paths and no incomplete dependency, so safe to author in parallel. Verification still needs the single Unity slot.
- **[U]**: needs the `unity` lease (`unity,emulator` when the worktree's `Library/` may target Android). **[E]**: needs the emulator. See [quickstart.md](quickstart.md) › Leases per step.
- Paths are repository-relative. The Core rules live in `Assets/IdleFactory/Core/Run/`.

---

## Phase 1: Setup and design gates

**Purpose**: Baselines and design records that later work depends on.

- [ ] T300 [P] Performance Engineer (Architect lens), plan step 0, #353 Option 0 [U][E]:
  - **Measure** W003-B on `a41c53f` in a separate base worktree. Run the Windows Mono development player on a quiet host (no editor or emulator process during the timed window; record the process list and the window). Take the median of 5 per run over at least 3 runs, record the spread, and attribute time to phases with Stopwatch accumulators (step scan, `CompletionPass`, run predicate, `AutoSell`, lookups).
  - **Emulator**: measure W003-B on the emulator from a development player with the test catalog.
  - **Record**: write `specs/005-economy-contracts/reviews/w003b-measurement.md` with the decision on Option 1 (index-based hot loop) or no change. Option 3 is out of scope.
  - **Gates**: this record gates T315 and T325 (research F5-R02, F5-R18 PF-3).
- [ ] T301 [P] Game Systems/Economy (author) with Game Designer/Product Owner review, plan step 13 (design part), writes `specs/005-economy-contracts/reviews/balance-policy-r2.md`. Content:
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

- [ ] T302 Gameplay Developer (Architect, Test/QA lenses), plan step 1a [U]:
  - **Baseline first**: add `Assets/IdleFactory/Tests/EditMode/Run/PublicApiSnapshotTests.cs` and a committed reflection baseline of the public surface of `IdleFactory.Core` (`Assets/IdleFactory/Tests/EditMode/Fixtures/PublicApi/core-public-api.txt`) in the first commit (QA-16).
  - **Split**: turn `Assets/IdleFactory/Core/Run/Application/RunPersistence.cs` into a `partial class`: lifecycle core and admission table kept whole, plus `RunPersistence.ColdStart.cs`, `RunPersistence.Transition.cs`, `RunPersistenceTypes.cs` (outcome, status and reasons), `OfflineReturn.cs` (`OfflineRunReturnReconciler`, `NoCredit`) and an internal `SafeRunStore.cs`.
  - **Acceptance**: no behaviour change, the snapshot is unchanged, all suites are green (research F5-R01).
- [ ] T303 Gameplay Developer (Architect, Test/QA lenses), after T302, plan step 1b [U]:
  - **Split**: `Assets/IdleFactory/Core/Run/Application/RunSnapshots.cs` into `RunSnapshots.Factory.cs`, `RunSnapshots.Economy.cs`, `RunSnapshots.Contracts.cs` and `RunSnapshots.Items.cs` (existing files merged in), plus `RunDerivation.cs`.
  - **Acceptance**: the snapshot is unchanged and all suites are green.
- [ ] T304 Gameplay Developer (Architect, Security, Test/QA lenses), after T303, plan step 1c [U]:
  - **Visibility**: make the command-path `RunState` mutators `internal`, deriving the list from a compile (AR-8). Every method used by `SaveV2Mapper` or another restore path stays `public` (`TryAddMachine`, `TrySetAutoSell`, `Inventory.TryImport`, `PermanentState.TryAddItems`, `TryRestoreInvestment`, `TrySetResearchLevel`, `TryAddEffect`, `TrySetIndustryLevel`, `ContractBoard.TryRestoreActive`, `TrySetCompletionCount`).
  - **Test access**: add `InternalsVisibleTo("IdleFactory.PlayModeTests")` in `Assets/IdleFactory/Core/Properties/AssemblyInfo.cs` (or the existing AssemblyInfo).
  - **Acceptance**: the snapshot diff is visibility-only, and Edit Mode, Play Mode and the Windows player suite are green.

### Follow-ups and seams

- [ ] T305 [P] Gameplay Developer (Architect, UI/UX lenses), after T304, plan step 2a, #355 [U]:
  - **Contract table**: create `docs/contracts/run-reason-codes.md` with the stable code table from `contracts/run-application-f005.md` (F003 codes plus `command.invalid`, `checkpoint.content` and `discard.unavailable`). F005 codes are added by the tasks that introduce them.
  - **Pin test**: add `Assets/IdleFactory/Tests/EditMode/Run/ReasonCodeContractTests.cs`. It reflects over `RunReasons` and the persistence reason classes, parses the table, and asserts set equality and that `RunRejectionCopy` has copy for each rejection code. A negative test uses an injected unknown code (QA-15).
  - **Links**: point the F003 contract to the new file.
- [ ] T306 [P] Gameplay Developer (Security, Architect, Test/QA lenses), after T304, plan step 2b, #354 [U]:
  - **Interval guard**: add a minimum-interval guard to desktop focus-loss safety checkpoints in `RunPersistence` (reading `runStart.liveGapThresholdMs` once T312 lands; a temporary constant of 60,000 ms before that). Quit and prestige checkpoints bypass the guard (SEC-5).
  - **Exception handling**: lifecycle callbacks catch every exception.
  - **Tests**: add Play Mode tests in `Assets/IdleFactory/Tests/PlayMode/RunDesktopLifecycleTests.cs`. They cover a failing checkpoint write through `RunLifecycleAdapter` (the run stays live, nothing is published, and the next save succeeds), the quit-then-focus-lost order, the interval and the bypasses.
- [ ] T307 [P] Gameplay Developer (Architect, Security lenses), after T304, plan step 2c [U]:
  - **Port**: add `Assets/IdleFactory/Core/Run/Application/IRunEventSink.cs` with a no-op default on `RunSession`, and `RunEvents.cs` with readonly allowlisted event structs per the data-model › Session log records.
  - **Constraint**: content IDs and enums only, no strings beyond IDs.
  - **Tests**: the sink is optional and its absence changes nothing (Edit Mode).

### Tooling (D-B; may start after T304 in parallel with T305–T307)

- [ ] T308 [P] DevOps/Operations (primary) with Gameplay Developer, Architect and Security lenses, after T304, plan step 3a [U]:
  - **Assembly**: create `Assets/IdleFactory/DevTools/IdleFactory.DevTools.asmdef` (`defineConstraints: ["IDLEFACTORY_DEV"]`, references `IdleFactory.Unity`, never referenced back).
  - **Seam**: add a null-default `ITimeScaleSource` seam in `Assets/IdleFactory/Unity/Bootstrap/FactoryRuntimeHost.cs` with the S_max clamp of 10 simulated seconds per frame. Max = the clamp, and the effective multiplier is reported.
  - **Overlay**: a DevTools overlay (×1/×10/×60/×600/Max, keyboard-operable) and a persistent text marker "ACCELERATED ×N" whose accessible name includes the factor.
  - **Dev save root (D-N)**: `persistentDataPath/IdleFactory-Dev/` under the define.
  - **Builds**: development build variants in `Assets/IdleFactory/Editor/PlatformBuildCommands.cs` use `BuildPlayerOptions.extraScriptingDefines`, never ProjectSettings. Add the new `BuildAndroidArm64Release` (non-development, unsigned for the scan).
  - **Tests**: the clamp and the seam through a fake in Play Mode, and the DevTools UI and dev save root in a separate define-gated test assembly `Assets/IdleFactory/Tests/DevTools/` (AR-10, OPS-1, OPS-3).
- [ ] T309 [P] DevOps/Operations (Security lens), after T308, plan step 3a (scan part): create `scripts/check-release-build.ps1`.
  - **Windows**: on a non-development build, check the `Managed/` assembly list for the DevTools assembly.
  - **Android**: on a release APK, extract with `System.IO.Compression` and scan `global-metadata.dat` for DevTools assembly and type names.
  - **Guard**: assert `runInBackground: 0` in `ProjectSettings/ProjectSettings.asset`.
  - **Exit codes**: 0, or 1 with findings. Manifest checks are added by T337 (research F5-R18, SEC-6, OPS-4).
- [ ] T310 [P] DevOps/Operations, after T304, plan step 3b: extend `scripts/test-unity.ps1` with `-Category <name>`, mapped to Unity `-testCategory`. Reject it together with `-StandaloneWindowsPlayer` (OPS-2). Verify with one `-Suite EditMode -Category` run [U].
- [ ] T311 [P] Gameplay Developer (Game Systems, Test/QA lenses), after T304, D-B strategy framework, #357 part 1 [U]:
  - **Framework**: add `IStrategyPolicy` and a runner extension in `Assets/IdleFactory/Tests/EditMode/Balance/` that runs strategies for runs 1–5 over 5 look-grid offsets with medians per run index.
  - **First strategies**: implement those that need no F005 command (Ore-only, contracts-only on the existing contracts, chain with the current reference policy, idle-heavy, casual with a pinned constant). Record their output on content r2 as a report (no new asserted bands).
  - **Runtime**: measure the runtime per strategy and offset, and project the 9-strategy matrix (PF-15).
  - **Purpose**: this is the Play-Tester's strategy channel from the first gameplay batch.

### Content schema v3 (F5-R03, F5-R17)

- [ ] T312 Gameplay Developer (Architect, Game Systems, Test/QA lenses), after T304, plan step 4a [U]:
  - **Definitions**: extend the definitions in `Assets/IdleFactory/Core/Run/Content/` (`RunStartDef`, `ContractOfferDef`, `MachineDef`, `PrestigeDef`, `IndustryUpgradeDef`, `RunCatalog`) with the v3 fields from data-model.md › Content, as optional constructor parameters with legacy defaults. The fields are inert in this task.
  - **Test helper**: add `Assets/IdleFactory/Tests/EditMode/Run/TestCatalogBuilder.cs` so test call sites change once.
  - **Acceptance**: the catalog and builder carry the new fields, all suites are green, and no behaviour changes.
- [ ] T313 Gameplay Developer (Architect, Security, Game Systems, Test/QA lenses), after T312, plan step 4b [U]:
  - **Reader**: evolve `Assets/IdleFactory/Unity/Content/RunContentReader.cs` and its DTOs in place to `contentSchemaVersion 3`, strictly. The dispatcher keeps `content.version.unsupported` for other versions.
  - **Shipped content**: migrate both shipped copies to schema v3 with revision 2 values and the F003 offer IDs `contract.plate`, `contract.gear` and `contract.timed-bar`. The copies are `Assets/IdleFactory/Content/Iron/iron-factory-content.json` and `Assets/IdleFactory/Tests/PlayMode/Fixtures/SaveV1/Resources/RunPlayerFixtures/iron-factory-content.json`, plus their parity tests.
  - **Schema and docs**: rename and update `Assets/IdleFactory/Content/Schemas/gameplay-content-v2.schema.json` to `-v3`, and update `docs/content/json-content-pipeline.md`.
  - **Tests**: port the strictness and canonical tests. Give the V2 device sets a frozen r2 catalog (`ShippedDeviceSets`, `DeviceSetShippedCatalogTests`). Prove V1 import on the v3 catalog.
  - **Goldens**: regenerate `trace-r2.csv` once as a labelled transitional golden where outputs change. Conservation and SC-001 gates stay asserted, and F003 pacing bands are reported, not asserted, until T339.
- [ ] T314 Gameplay Developer (Game Systems, Security, Test/QA lenses), after T313, plan step 4c [U]: add the structural content rules with rule-code tests in `Assets/IdleFactory/Unity/Content/RunContentValidator.cs`: `sell.steps`; `live-gap.range` (1,000 ≤ value ≤ 28,800,000); `contract.bonus.fields`; `contract.ratio.*` ranges (gated on `contentRevision ≥ 3`); `contract.quantity.cap`; `contract.produce.min`; `milestone.order`; `build.base.bound` (base and cap ≤ 10^12); `cost.floor`; `speed.level.dead`.
- [ ] T315 Parent verification gate after T313 and T314 (QA-1) [U]: run the full Play Mode suite and `scripts/test-unity.ps1 -StandaloneWindowsPlayer`, plus a console check. Record the result in the batch notes. A failure blocks Phase 3.

**Checkpoint**: Foundation ready. The user-story phases may begin.

---

## Phase 3: User Story 1 — Sell stock by hand (P1) 🎯 MVP

**Goal**: The player sells chosen quantities at the same price as auto-sell, with no Factory Value gain (FR-001–FR-003).

**Independent Test**: With auto-sell off, sell 1, 10, 100 and All. Check exact Cash and stock deltas, no partial change on rejection, and full-stock FV neutrality.

- [ ] T316 [US1] Gameplay Developer (Game Systems, Performance, Architect, Test/QA lenses; Medium at `high` reasoning), after T315 and T300 (applies Option 1 first if T300 chose it), plan step 5 [U]:
  - **Pricing**: add `Assets/IdleFactory/Core/Run/Domain/SalePricing.cs` (`MulDivFloor(units × base, salePricePpm, 10^6)`, checked) and route `RunSimulator.AutoSell` (one sale per resource per instant, after the instant's expiries) and `FactoryValuation` through it.
  - **Rule**: add the `chain.dominance` rule (efficiency monotone along the chain and terminal > Ore × (1 + margin), revision ≥ 3) with tests.
  - **Tests**: `SalePricingTests` and the equivalence case (q) for conservation per step.
  - **Goldens**: name every golden regenerated.
- [ ] T317 [US1] Gameplay Developer (Game Systems, Test/QA lenses), after T316, plan step 5 [U]:
  - **Commands**: add `Sell(resourceId, quantity)` and `SellAll(resourceId)` in `Assets/IdleFactory/Core/Run/Application/RunCommands.Sell.cs`. The order is lifecycle, `resource.locked`, `sell.quantity-invalid`, `stock.insufficient`, then `value.limit`. The `SettleFits` pre-check subtracts the sold stock.
  - **Snapshot and events**: add `SellSteps` and `SellPreview` to `RunSnapshots.Economy.cs`, and raise the sale events through `IRunEventSink`.
  - **Contract table**: add `sell.quantity-invalid` to `docs/contracts/run-reason-codes.md`.
  - **Tests** (`RunSellCommandTests`):
    - each rejection, with no partial change;
    - full-stock FV neutrality, and no sale raising FV;
    - sell during a full-store block (QA-5);
    - 10^18 −1, = and +1 boundaries (QA-6).
- [ ] T318 [US1] Performance Engineer, after T317: re-measure W003-B with and without auto-deliver absent (baseline variant) on the same hashes, apply the 10 % regression guard against the post-#356 baseline, and append the result to `reviews/w003b-measurement.md` (PF-3, PF-10) [U].
- [ ] T319 [US1] UI/UX Developer (UI/UX Reviewer, Game Designer, Test/QA lenses), after T317, plan step 14a [U]:
  - **UI**: in `MarketPanelHost` and the inventory row presenter under `Assets/IdleFactory/Unity/UI/`, add the step selector (`inventory-sell-step-<resourceId>`) and the Sell button (`inventory-sell-<resourceId>`) per `contracts/ui-surfaces-f005.md`.
  - **States**: "Nothing to sell" or "Locked" rows; "Need N" without silently changing the step; the All confirmation only when an active delivery contract would fall short; a success notice with the exact value `N × $p = $Y`; re-render on stale taps with focus kept.
  - **Focus**: per-row focus order step → Sell → Auto-sell.
  - **Names**: accessible names per the name pattern table.
  - **Tests**: `MarketSellPanelTests` covering the control × state × screen-class rows for Sell in the QA-12 table below.

**Checkpoint**: US1 is independently functional.

---

## Phase 4: User Story 2 — Contracts are a choice, not a substitute (P1)

**Goal**: Ratio rewards with headroom, cooldowns, bonus offers, production objectives and one Item ladder (FR-004–FR-009, FR-007a).

**Independent Test**: For every offer and modifier combination, the reward is within its bounds. Cooldown follows every ending. A production objective is not advanced by trade. Ladder grants follow the run-wide index.

- [ ] T320 [US2] Gameplay Developer (Game Systems, Architect, Test/QA lenses), after T317, plan step 6a [U]:
  - **Rewards**: add `Assets/IdleFactory/Core/Run/Domain/ContractRewards.cs` with `Grant` and `CheckGrant`. The Cash reward is `MulDivFloor(SalePricing.Value(required, basePrice, salePricePpm), rewardRatioPpm, 10^6)`, with modifiers read at the completion instant (FR-009).
  - **Delivery path**: move `RunState.TryDeliverContract` onto it.
  - **Item ladder**: grant when the pre-increment run-wide completion index equals the entry index, up to `perRunCap`. Production completions count.
  - **Completion total**: keep `runCompletionTotal` in `ContractBoard`.
  - **Rules**: add `contract.quantity.capacity`, `contract.ratio.produce` (R_prod + max standing R for the resource ≤ 2.0) and `ladder.uptime` (`perRunCap × duration ≤ uptimeMaxPpm × minRunDurationMs`).
  - **Tests**: `ContractRewardsTests`, including Research bought between accept and delivery (ECO-2), the 1024-index bound and the reward 10^18 boundary.
- [ ] T321 [US2] Gameplay Developer (Game Systems, Test/QA lenses), after T320, plan step 6b [U]:
  - **Board**: in `Assets/IdleFactory/Core/Run/Domain/ContractBoard.cs`, add cooldowns started on every ending (delivery, abandon, expiry) as UTC stamps evaluated lazily, bonus-offer state (no trigger yet), the `Version` counter included in `RunSession.Publish` tokens, and the FR-007a prestige reset.
  - **Accept**: `AcceptContract` checks `contract.cooldown`, `contract.bonus.unavailable` and `contract.capacity` (required > current capacity).
  - **Snapshot**: add the fields per the contract (`Objective`, `Class`, `CooldownRemainingMs`, `BonusWindowRemainingMs`, `Deliverable`).
  - **Contract table**: add the three codes to `docs/contracts/run-reason-codes.md`.
  - **Tests**:
    - `ContractCooldownTests`: 3 endings × live and offline × exact `readyAt` (QA-9);
    - `BonusOfferTests` (state);
    - prestige reset cases: cooldown running, auto-deliver on, produced, bonus triggered (QA-8).
- [ ] T322 [US2] Gameplay Developer (Game Systems, Test/QA lenses), after T320, plan step 6c [U]: add production-objective state (`Produced`) and command paths. `DeliverContract` returns `command.invalid` for production objectives, and `AbandonContract` starts the cooldown. Tests in `ProduceObjectiveTests`: trade never advances the objective (completion counting comes in T327).

**Checkpoint**: US2 contract rules work at the command level. Auto-completion comes with US3.

---

## Phase 5: User Story 4 — Prices grow with the factory (P1)

**Goal**: Build prices scale with the owned count and the number of completed prestiges, starting Cash grows per prestige, and the displayed price equals the charged price (FR-013–FR-017).

**Independent Test**: Build successive machines, remove and rebuild, and prestige. Every shown price equals the oracle and the charged price, and Cash + FV never rises from remove/rebuild.

- [ ] T323 [P] [US4] Gameplay Developer (Game Systems, Test/QA lenses), after T315, plan step 8 [U]:
  - **Pricing**: add `Assets/IdleFactory/Core/Run/Application/BuildPricing.cs` with the algorithm of research F5-R06: early cap return, per-step `MulDivFloor`, a final ceiling, then the F003 discounts with round-up.
  - **Starting Cash (D-J)**: `startingCashPrestigeGrowthPpm` with the same iterated algorithm.
  - **Command and cache**: `Build(slot, machineId, long? expectedPrice = null)` rejects with `build.price.changed` on a mismatch. Cache the price per structure revision in `RunSnapshots.Factory.cs`.
  - **Rules**: `build.price.cap` and `build.start.affordable` (zero discounts and zero Seed Capital, at every index ≤ `maxIndex`).
  - **Contract table**: add `build.price.changed` to the reason table.
  - **Tests**: `BuildPricingTests` with a BigInteger oracle and a random-parameter property test, ECO-7 remove/rebuild, and the 10^18 build boundary.
- [ ] T324 [US4] UI/UX Developer (UI/UX Reviewer, Test/QA lenses), after T323, plan step 14c (build part) [U]:
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

- [ ] T325 [US3] Gameplay Developer (Architect, Performance, Test/QA lenses; Medium at `high` reasoning), after T321, T322 and T300, plan step 7a-1 [U]:
  - **Mapping**: add `Assets/IdleFactory/Core/Run/Simulation/TimelineMapping.cs` (origin ms, sub-ms tick phase, `num/den`, a single floor rule).
  - **Wiring**: route `RunSession.TimelineUtcMs`, `OfflineSegmenter` and `RunSimulator.Advance(activeTicks, mapping, boundaryEvents)` through it, keeping a legacy overload. The mapping is called per advance and per stamp, never per completion (PF-6).
  - **Acceptance**: bit-identical results against the W003 fixtures and hashes, with live and offline stamps asserted (AR2-1).
- [ ] T326 [US3] Gameplay Developer (Architect, Game Systems, Performance, Test/QA lenses; Medium at `high` reasoning), after T325, plan step 7a-2 [U]:
  - **Scratch state**: in `RunSimulator`, add scratch `_rp`, contract state and Item grants, all committed at the end of the advance.
  - **Settle loop**: completions → auto-delivery (at most once per timestamp) → auto-sell keeping `min(stock, required)` for every active delivery contract (D-L).
  - **Pass budget**: N + 2, fail-closed at the bound with a diagnostic (PF-1).
  - **Expiry**: the deadline boundary is at deadline + 1 tick.
  - **Blocked delivery**: `value.limit` blocks with no change, sets the blocked state in the snapshot, and retries at later instants. The event fires only on state change (AR2-4).
  - **Command**: add `SetAutoDeliver` with the `DeliverContract` worst-case pre-check.
  - **Tests**: (r) `SetAutoDeliver` delivers at t = 0, and (s) a block retries.
- [ ] T327 [US3] Gameplay Developer (Architect, Game Systems, Test/QA lenses), after T326, plan step 7b [U]:
  - **Production**: count production in the completion pass (O(1)).
  - **Bonus**: trigger in the unlock pass, stamping the window end as `RealUtc(t) + window`.
  - **Availability**: cooldown and bonus availability are evaluated lazily, with no boundary events.
  - **Outputs**: add `LastOutcome` and offline-summary fields (`AutoDelivered`, `ProduceCompleted`, `Expired`, reward totals), and session events from outcome and board-version diffs.
  - **Tests**: production completion and the once-per-run bonus.
- [ ] T328 [US3] Test/QA-minded Gameplay Developer (Architect, Test/QA lenses), after T327, plan step 7c [U]: in `Assets/IdleFactory/Tests/EditMode/Run/AutoDeliverEquivalenceTests.cs`, add equivalence cases (l)–(s) from plan › Event order:
  - auto-delivery across live partitions, including deadline ±1 tick and sub-ms phase carry;
  - production across partitions;
  - the reserve;
  - identical cooldown and bonus stamps;
  - live gap equivalence (placeholder until T335);
  - conservation;
  - t = 0 delivery;
  - a blocked retry.
- [ ] T329 [US3] Performance Engineer, after T325, T326 and T327: re-measure W003-B with and without auto-deliver after each of T325, T326 and T327 on the same hashes. Record the realized maximum passes per timestamp and append to `reviews/w003b-measurement.md` (PF-1, PF-3) [U].
- [ ] T330 [US3] UI/UX Developer (UI/UX Reviewer, Game Designer, Test/QA lenses), after T327, plan step 14b [U]: in the contracts panel and the shell tab under `Assets/IdleFactory/Unity/UI/`, add per `contracts/ui-surfaces-f005.md`:
  - `contracts-autodeliver` (delivery objectives only, Off after accept, hint "applies to this contract only");
  - the auto-delivered notice and the short-stock expiry line;
  - `contracts-offer-cooldown-<offerId>` on the once-per-second tick, with a bonus-ended transition that does not jump the layout;
  - `contracts-produce-progress`;
  - `tab-contracts-badge` ("Contracts (n)" or "!", "•" on compact), cleared when nothing is deliverable;
  - Accept rejection copy for `contract.cooldown`, `contract.bonus.unavailable` and `contract.capacity`.

  Tests: `ContractsAutoDeliverPanelTests` per the QA-12 rows.

**Checkpoint**: US3 is functional live and offline.

---

## Phase 7: User Story 5 and User Story 6 — Next goal and stronger runs (P2)

**Goal**: A rising threshold clamped at `maxIndex`, display-only milestones, and Industry with diminishing returns (FR-018–FR-021).

**Independent Test**: The threshold per index equals the oracle and stays constant beyond `maxIndex`. The goals snapshot covers every state. The Industry tables match the per-kind decay. A retry advances the index once.

- [ ] T331 [P] [US6] Gameplay Developer (Game Systems, Architect, Test/QA lenses), after T315, plan step 9 [U]:
  - **Threshold**: add `Assets/IdleFactory/Core/Run/Application/PrestigeThreshold.cs`, iterated floors with a final ceiling. The index is `min(PrestigeCount, maxIndex)` (D-K). Preview and confirm read it, and `PrestigePreview` adds `Threshold`, `NextThreshold` and `PrestigeIndex`.
  - **Industry**: per-kind diminishing tables in `Assets/IdleFactory/Core/Run/Content/IndustryUpgradeDef.cs` (multiplicative increment, discount decrement, additive amount; the cumulative fold keeps the `FoldLevels` floor).
  - **Rules**: `prestige.threshold.bound` and `industry.ceiling.level`.
  - **Tests**: `PrestigeThresholdTests` (oracle; a retry or interruption advances the index once) and `IndustryDiminishingTests`.
- [ ] T332 [US5] Gameplay Developer (Game Systems, Test/QA lenses), after T331, plan step 9 (goals): add `Assets/IdleFactory/Core/Run/Application/RunSnapshots.Goals.cs` (`GoalsSnapshot`: next milestone or the threshold when content defines none, target, reached, floored percentage, Factory Sale available, all reached). The cache is keyed on milestone crossing (PF-8). Raise the milestone event. Tests in `GoalsSnapshotTests` [U].
- [ ] T333 [US5] UI/UX Developer (UI/UX Reviewer, Test/QA lenses), after T332 and T331, plan step 14c (goals and prestige part) [U]:
  - **Goal line**: `goals-next` at the top of Market (Market-only, a known limitation for F004) with a one-time notice.
  - **Prestige panel**: `prestige-threshold` shows the current and next threshold and the index.
  - **Tests**: `GoalsLinePanelTests` per the QA-12 rows.

**Checkpoint**: US5 and US6 are functional.

---

## Phase 8: Save V3 (persistence for US2, US3 and US6)

**Goal**: Save V3 reads V2, strict and fail-closed, with contract-scoped content drift (FR-034, SC-006).

**Independent Test**: Every V2 fixture loads into an equivalent V3 state. Every malformed V3 fixture is rejected without change.

- [ ] T334 [US3] Gameplay Developer (Architect, Security, Test/QA lenses), after T327, plan step 10a [U]:
  - **Mapper**: version-parametrize `Assets/IdleFactory/Unity/Persistence/SaveV2Mapper.cs` with per-version member tables chosen at the `schemaVersion` peek. `ValidateBytes` checks the version read.
  - **Writer**: always writes V3, only after a successful load or a fresh start.
  - **V3 members**: `activeContract.autoDeliver`, `activeContract.produced`, `contractCooldowns[]`, `bonusOffers[]` (`windowEndUtcMs` required for both states) and `runCompletionTotal`.
  - **Restore setters**: `ContractBoard` `TryRestoreCooldown`, `TryRestoreBonus` and `TryRestoreProduced`, public, with the API baseline regenerated and architect-reviewed.
  - **Rename**: an optional rename to `SaveMapper` happens only as a separate mechanical commit.
- [ ] T335 [US3] Gameplay Developer (Security, Test/QA lenses), after T334, plan step 10b [U]:
  - **Content drift**: drop only contract-scoped entries (active, cooldowns, bonus, `completionCounts`) of unknown offers and report `contract.dropped`. `runCompletionTotal` is preserved, and a test proves no Item is re-granted (SEC-8).
  - **Fail-closed matrix**: per the QA-11 table below.
  - **V2 regression**: every existing V2 fixture (device sets, W003, `prestige-ready`, import outputs).
  - **Fault injection**: a failed first V3 write after a V2 load keeps V2 readable.
  - **Forward compatibility**: `schemaVersion` 4 is refused without any write or V1 import (SEC-14).
  - **Contract table**: add `contract.dropped`.
  - **Tests**: `SaveV3*Tests` and fixtures under `Assets/IdleFactory/Tests/EditMode/Fixtures/SaveV3/`.
- [ ] T336 [US3] Performance Engineer (Game Systems lens), after T335 and T331: create the `W005-L` Save V3 fixture near the run-3 threshold (12 slots, chain mix, auto-deliver active, auto-sell on end products) with its hash, plus a StandalonePlayer `Resources` copy and a SHA-256 parity test. Take an early Windows timing on a quiet host recording completions, timestamps, passes per timestamp, V3 write time and size. Record in `reviews/w005l-measurement.md` (QA-2, PF-11, PF-16) [U].

---

## Phase 9: User Story 9 — Waking from sleep is an offline return (P2)

**Goal**: A wall gap above the threshold becomes a standard offline return (FR-032, FR-033).

**Independent Test**: A gap above the threshold gives the same result as a quit and restart. Gaps at or below the threshold run live. A negative gap credits nothing.

- [ ] T337 [US9] Gameplay Developer (Architect, Security, Test/QA lenses), after T306, T308, T325 and T334, plan step 11 [U]:
  - **Clock**: an explicit UTC read in `FactoryRuntimeHost`.
  - **Suspension**: a `RunPersistence.RecordSuspension(atUtcMs)` overload through `Admit`, including a pending prestige. The departure is `max(current, min(atUtcMs, timelineEnd))` and is never later than the current UTC read (SEC-10).
  - **Gap path**: `ResumeAndReconcile` follows. Only the gap path calls the overload.
  - **Time scale**: applied after the gap check.
  - **Tests** (`LiveGapTests`): threshold −1, = and +1 ms; a negative clock; accelerated frames not counting; a checkpoint inside a gap; a gap during a pending prestige; the equivalence case (p) in `AutoDeliverEquivalenceTests`.

---

## Phase 10: User Story 8 — Local session log (P3)

**Goal**: Allowlisted, bounded, failure-isolated local log with export, toggle and clear (FR-028–FR-031, SC-008, D-M).

**Independent Test**: A session with a sale, a contract, a prestige and an offline return exports a valid log within the bound, containing only schema fields. A forced write failure leaves play and saving unaffected.

- [ ] T338 [US8] Gameplay Developer (Security, Performance, Test/QA lenses), after T307 and T335, plan step 12b [U]: add `Assets/IdleFactory/Unity/Persistence/SessionLogWriter.cs` and `SessionLogExport.cs`.
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
- [ ] T339 [US8] DevOps/Operations (Security lens), after T338 and T309, plan step 12c [U][E]:
  - **Backup rules**: add Android backup rules (`fullBackupContent` and `dataExtractionRules` XML excluding `SessionLog/` and `Exports/`) to the existing `Assets/Plugins/Android/AndroidManifest.xml` and `res/xml`. No FileProvider and no Gradle template (D-M).
  - **Release check**: extend `scripts/check-release-build.ps1` with the merged-manifest check (`allowBackup`, both attributes, the XML exclusions; via `Temp/gradleOut` or `aapt2` with the SDK path from config).
  - **Build check**: build the GLES3 emulator APK and confirm it still runs.
  - **Settings**: `git diff ProjectSettings/` is empty except for deliberate changes (OPS-6).
- [ ] T340 [US8] UI/UX Developer (UI/UX Reviewer, Security, Test/QA lenses), after T338 and T308, plan step 14d [U]:
  - **Session-log section**: add `session-log-section` as the fourth `MarketPanelHost` section with the toggle, export, clear (confirmation "Clear the session log? This cannot be undone", cancelled by rotation) and a persistent "Last export" line (selectable on Windows, never logged).
  - **DevTools overlay**: polish for keyboard use and safe areas.
  - **Tests**: `SessionLogPanelTests` per the QA-12 rows.

---

## Phase 11: User Story 7 — Strategy trace, balance r3 and time acceleration (P2)

**Goal**: Policy r2 and the full strategy matrix with asserted gates, tuned content revision 3, and Play-Tester reports per batch (FR-022–FR-027, SC-002, SC-004, SC-010).

**Independent Test**: `BalanceFull` runs within its budget, reproduces, and passes the FR-024 gates on medians. Acceleration is marked and absent in release.

- [ ] T341 [US7] Gameplay Developer (Game Systems, Test/QA, Performance lenses), after T301, T311, T317, T327, T323, T331 and T310, plan step 13 [U]:
  - **Policy and strategies**: implement reference policy r2 per `reviews/balance-policy-r2.md` and the remaining strategies (stop-at-Bar, stop-at-Plate, market-only chain, prestige-later) in `Assets/IdleFactory/Tests/EditMode/Balance/`.
  - **Ensemble and gates**: the 5-offset ensemble (reduced if the PF-15 projection exceeds 20 min) and the FR-024 gates in `BalanceTraceTests` (fast gate: chain and Ore-only, 1 offset, runs 1–2) and `BalanceTraceFullTests` (`[Category("BalanceFull")]`).
  - **4.0× rule**: the trace gate over the max IP of the at-threshold and prestige-later strategies.
  - **Price variant**: the non-rounding variant replaces `var.price-80`, with a test that the old path is gone (QA-14).
  - **Output**: `trace-r3.csv`, regenerated only on opt-in.
- [ ] T342 [US7] Play-Tester (read-only; Medium, sonnet) for gameplay batch A (after US1, US2 and US4 merge: T319, T322, T324), owner reviews:
  - **Strategy runs**: through the T311 framework.
  - **Player session**: a session in a Windows build with screenshots.
  - **Report**: write `specs/005-economy-contracts/reviews/playtest-agent-batch-a.md` with measurements and labelled feel inferences. Each finding has evidence, severity and a proposed disposition (SC-010).
- [ ] T343 [US7] Play-Tester (read-only; Medium, sonnet) for gameplay batch B (after US3, US5, US6 and US9 merge: T330, T333, T337), owner reviews:
  - **Strategy runs and session**: strategy runs plus an accelerated development-player session (marker visible in screenshots).
  - **Report**: `reviews/playtest-agent-batch-b.md`.
- [ ] T344 [US7] Gameplay Developer (Test/QA, UI/UX lenses), after T319, T324, T330, T333 and T340, plan step 15, QA-1 [U]: convert remaining hard-coded content values in the Play Mode panel and journey tests under `Assets/IdleFactory/Tests/PlayMode/` to catalog-derived values. Journeys assert shape (run 2 not slower, threshold rises), never seconds.
- [ ] T345 [US7] Game Systems/Economy (author) with Game Designer/Product Owner review, after T341 and T344, plan step 16 [U]:
  - **Tuning record**: write `reviews/balance-trace-r3.md` with the objective and order, a closed-form fluid pre-solve, the threshold → IP → 4.0× circularity, a Seed Capital replacement, and the re-derivation of the `contracts-off` and `returns` bounds (tolerances only, D-A).
  - **Content revision 3**: tune `contentRevision` 3 in both shipped copies (ratios, cooldowns, build prices, starting-Cash growth, threshold growth, milestones, Industry decay, ladder, recipes or prices for `chain.dominance`), with all revision ≥ 3 rules passing.
  - **Fixture**: regenerate `W005-L`, recording both hashes.
  - **Runs**: full Play Mode, the Windows player and `BalanceFull`.
- [ ] T346 [US7] Play-Tester (read-only; Medium, sonnet) for gameplay batch C (after T345), owner reviews:
  - **Strategy runs**: on revision 3.
  - **Long session**: an accelerated development-player session to the second prestige.
  - **Report**: `reviews/playtest-agent-batch-c.md`.

---

## Phase 12: Player and device evidence

- [ ] T347 Test/QA (independent), after T345, plan step 17a [U]: full Edit Mode and Play Mode suites with 0 skips and a console check. Record in `reviews/full-suite-evidence.md`.
- [ ] T348 Gameplay Developer (Test/QA lens), after T345, plan step 17b [U]: `[Category("StandalonePlayer")]` tests in `Assets/IdleFactory/Tests/PlayMode/`:
  - `EconomyJourneyPlayerTests`: sell → contract with auto-deliver → prestige at the rising threshold → next-run build price and starting Cash, shape assertions only;
  - a V2→V3 real-store round trip;
  - `W005-L` and V3 fixture SHA-256 parity;
  - log write-failure isolation through the test composition seam (QA-13).

  Run `scripts/test-unity.ps1 -StandaloneWindowsPlayer`.
- [ ] T349 Performance Engineer, after T345 and T348, plan step 17c [U]:
  - **W005-L return**: the 8 h return on the Windows player on a quiet host, median of run medians, spread ≤ 10 %.
  - **Frame time**: Windows frame time with UI over 600 frames at `W005-L` (average and p95).
  - **Acceleration**: in a separate development-player run built with `IDLEFACTORY_DEV`, the acceleration p95 at ×600 and Max on W003-B with auto-deliver, plus frame time during acceleration. The emulator is evidence only.
  - **Platform**: IL2CPP is a recorded open item.
  - **Record**: update `reviews/w005l-measurement.md`. A miss opens a Performance review task with options.
- [ ] T350 DevOps/Operations, after T339 and T345, plan step 17d [U]: build a Windows non-development build and an Android release APK (`BuildAndroidArm64Release`), then run `scripts/check-release-build.ps1` on both. Record in `reviews/release-scan.md`. Repeat once per later batch.
- [ ] T351 Test/QA (independent), after T345 and T339, plan step 17e [U][E]: extend `scripts/android-emulator-journey.ps1` with F005 steps (sell, auto-deliver toggle, session-log export with the path shown, `adb pull` of the export) and the `W005-L` return (≤ 2 s) at 60 fps. Call it through `-Command "& ./scripts/android-emulator-journey.ps1 ..."`. Record as emulator-only evidence.

---

## Phase 13: Independent reviews, human gates and final acceptance

- [ ] T352 UI/UX Reviewer (independent of T319, T324, T330, T333 and T340 authors), after T340 and T344: review every F005 control against `contracts/ui-surfaces-f005.md` and the QA-12 table at every screen class, at 100 % and 200 % text, and with touch and pointer. Record in `reviews/ui-ux-review.md`.
- [ ] T353 Owner (human gate), after T345 and T346:
  - **Playtest**: a named human playtest on the shipped build that reaches the first prestige and a second run.
  - **Record**: write `reviews/playtest-<name>-<date>.md` with the exported session log, the human-to-trace ratio at the timeline samples, and the 1–5 answer to "Did both selling and contracts matter to your income?" (SC-005, #358).
- [ ] T354 Owner (human gate), after T353: approve or reject `contentRevision` 3 in `reviews/content-approval-r3.md` (FR-027).
- [ ] T355 Parent with independent reviewers (Game Designer/Product Owner, Software Architect, Test/QA, Game Systems/Economy, UI/UX, Performance, Security/Privacy, DevOps), after T347–T354: final verdicts on the exact accepted commit, recorded in `reviews/feature-review.md` and `reviews/final-acceptance.md`. Review waves respect `max_parallel: 3`.
- [ ] T356 Parent, after T355: closeout hooks and convergence.
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

## QA-12: Control × state × screen-class table (T319, T324, T330, T333, T340)

Every row runs at every `ShellScreens.All()` entry, at 100 % and 200 % text, with worst-case content (6-digit values, longest resource name, longest reason). Each row is checked with touch and pointer exact-value inspection, accessible-name assertions per state, and focus on the first, middle and last control.

| Control | States to assert |
|---|---|
| Sell selector and button | normal; nothing to sell; locked; "Need N"; stale tap re-render with focus kept; All confirmation (shortfall, then rotation cancel); success notice; each rejection copy (`resource.locked`, `sell.quantity-invalid`, `stock.insufficient`, `value.limit`) |
| Build price | affordable; "Need $X more"; capped; `build.price.changed` recovery; Remove confirmation with the rebuild price |
| Auto-deliver toggle | off and on; hidden for production with a hint; Off after re-accept; auto-delivered notice; short-stock expiry line; blocked state |
| Offer cooldown and bonus | countdown tick; bonus window end transition; Accept rejection copy (`contract.cooldown`, `contract.bonus.unavailable`, `contract.capacity`) |
| Production progress | progress; completion |
| Contracts badge | on with n; compact "•"; cleared after a sale or auto-sell |
| Goal line | next goal; reached notice; Factory Sale available; threshold-only (no milestones); all reached |
| Prestige threshold | current and next; at `maxIndex` (same value) |
| Session-log section | on and off; export path; nothing to export; export failure with retry; clear confirmation with rotation cancel; last export line |
| DevTools overlay (dev assembly tests) | each factor; marker text and name; no overlap at 200 % |

---

## Dependencies and execution order

- **Phase 1**: T300 runs in parallel with Phase 2 and gates T316 and T325. T301 gates T341.
- **Phase 2**:
  - T302 → T303 → T304.
  - After T304, these can be authored in parallel: T305, T306, T307, T308 (→ T309), T310, T311 and T312.
  - Then T312 → T313 → T314 → T315.
- **User stories after T315**:
  - US1: T316 → T317 → T318 and T319.
  - US2: T320 (after T317) → T321 and T322.
  - US4: T323 (∥) → T324.
  - US3: T325 (after T321, T322 and T300) → T326 → T327 → T328, T329 and T330.
  - US5/US6: T331 (∥) → T332 → T333.
  - Save V3: T334 (after T327) → T335 → T336.
  - US9: T337 (after T306, T308, T325 and T334).
  - US8: T338 (after T307 and T335) → T339 and T340.
- **US7**: T341 after T301, T311, T317, T327, T323, T331 and T310. T342 after batch A, T343 after batch B, T344 after the UI tasks, T345 after T341 and T344, T346 after T345.
- **Evidence and gates**: T347–T351 after T345. T352–T356 last.

## Batches (for MAQA)

| Batch | Tasks | Notes |
|---|---|---|
| 0 | T300, T301, T302–T304 | refactor serial; T300 and T301 in parallel |
| 1 | T305–T315 | follow-ups, tooling, strategy framework, content v3 |
| A | T316–T324 | US1, US2, US4. The Play-Tester report T342 is due before the batch closes. |
| B | T325–T340 | US3, US5, US6, Save V3, US9, US8. Play-Tester report T343. |
| C | T341, T344–T346 | balance r3 and tuning. Play-Tester report T346. |
| D | T347–T356 | evidence, reviews, gates, closeout |

Each batch ends with the after_implement hooks, the hook gate and the stale-worktree report. The parent then proposes a fresh session (AGENTS.md).

## Parallel Execution

After T304: author T305, T306, T307, T308, T310, T311 and T312 in parallel (up to `max_parallel: 3` agents at a time; Unity verification is serialized by the lease). After T315: T323 and T331 are independent of the US1/US2 chain. The UI tasks T319, T324, T330, T333 and T340 touch separate panel paths.

## Implementation Strategy

- **MVP**: Phases 1–2 plus US1 (manual selling).
- **Then**: US2 and US4 (contract headroom and price scaling) close the core economy problem.
- **Then**: US3, US5, US6 and US9 complete the loop.
- **Last**: US8 and US7 deliver the measurement and tuning that revision 3 and the human playtest need.
