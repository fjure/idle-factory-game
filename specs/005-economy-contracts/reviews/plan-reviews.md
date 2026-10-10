# Feature 005 plan reviews

Base of all reviews: plan commit `72874db`. Every reviewer was read-only and independent of the plan author (the parent).

## Wave A (2026-10-10)

| Lens | Model | Verdict |
|---|---|---|
| Software Architect | sonnet, high reasoning | APPROVE-WITH-CHANGES: 2 Blocker, 4 High, 7 Medium, 2 Low |
| Test/QA | sonnet | APPROVE-WITH-CHANGES: 4 High, 8 Medium, 4 Low |
| Gameplay Developer | sonnet | APPROVE-WITH-CHANGES: 4 High, 6 Medium, 3 Low; churn estimate; Issue splits |

### Dispositions

All findings were accepted. The applied changes are listed below. The artifacts changed are research.md, plan.md, data-model.md and contracts/.

| ID | Severity | Disposition |
|---|---|---|
| AR-1, GD-1 | Blocker/High | One `TimelineMapping` (origin ms, sub-ms phase, `num/den`) with a single floor rule is shared by session, segmenter and simulator. The `Advance` signature changes (F5-R05). Equivalence (l) and (o) cover phase carry and identical stamps. |
| AR-2 | Blocker | The order is restated against `ProcessTimestamp`: boundary events, then the settle loop (completions → auto-delivery → auto-sell with reserve), then the unlock pass with the bonus trigger. The pass bound is machines + 2. Test (r) pins `SetAutoDeliver` delivering at t = 0. |
| AR-3 | High | Cooldown and bonus windows are stored UTC stamps, evaluated lazily. They are not boundary events. |
| AR-4 | High | The simulation order decides the deadline race, with no real-time compare. The scratch contract fields and the Item-grant commit are specified. An overflow blocks auto-delivery without change and retries (test (s)). |
| AR-5, GD-10 | High/Medium | One version-parametrized mapper with per-version member tables. `ValidateBytes` checks the version read. Drops are limited to contract-scoped lists, including `completionCounts`. New `ContractBoard` restore setters. Files stay in `Checkpoints/v2/`. |
| AR-6 | High | The reader evolves in place. `content.version.unsupported` stays. Strictness tests are ported. A `TestCatalogBuilder` absorbs constructor churn. The V2 device sets use a frozen r2 catalog. The PlayMode copy and the schema change in the same unit. |
| AR-7, GD-13 | High/Low | Step 4 is split into 4a, 4b and 4c. Semantic rules land with their owning units. |
| AR-8 | Medium | The internal-mutator list is derived from a compile. Restore paths used by the mapper stay public. `InternalsVisibleTo` is added for PlayModeTests. |
| AR-9 | Medium | The #356 commit is visibility-only. Later units regenerate the baseline, and an architect reviews each diff. |
| AR-10 | Medium | The define is applied through `extraScriptingDefines` per build, never in ProjectSettings. The seam is tested with an `ITimeScaleSource` fake. DevTools UI tests go in a define-gated test assembly. |
| AR-11 | Medium | `RecordSuspension(atUtcMs)` is an overload through `Admit`, including a pending prestige. The host reads UTC explicitly. When the accelerated timeline runs ahead of the wall clock, the next offline credit is 0. |
| AR-12 | Medium | Step 0 runs in a base worktree in parallel with step 1. Only step 5 needs its record. |
| AR-13 | Medium | `IRunEventSink` lands as step 2c, and each unit emits its own events. ∥ is stated as authoring parallelism, with batched verification windows. |
| AR-14 | Medium | Offline summary and `LastOutcome` fields for auto-delivery. V1 import runs on the v3 catalog (4b). |
| AR-15 | Low | Accepted as a recorded trade-off: partial files reduce conflicts, not coupling. |
| AR-16 | Low | Reason codes move to `docs/contracts/run-reason-codes.md` (2a). |
| GD-2 | High | `SalePricing` and `ContractRewards` move to Domain. |
| GD-3 | High | Scratch `_rp`, contract state and Item grants with a common commit. `SetAutoDeliver` reuses the `DeliverContract` worst-case pre-check. |
| GD-4 | High | Per-kind decay: multiplicative increment, discount decrement, additive amount. A cumulative fold table keeps the `FoldLevels` floor. |
| GD-5 | Medium | The build-price algorithm is specified with an early cap return and `build.base.bound` (base ≤ 10^12). A BigInteger oracle checks it. |
| GD-6 | Medium | `Build(..., long? expectedPrice = null)`. The UI always passes the price. |
| GD-7 | Medium | `ContractBoard.Version` goes into the publish tokens. |
| GD-8 | Medium | The ladder index is the run-wide total of completed contracts. The cap counts ladder grants. Reset on prestige. |
| GD-9 | Medium | Reward Cash = `MulDivFloor(SalePricing.Value(required, base, salePricePpm), ratioPpm, 10^6)` on all paths. |
| GD-11, GD-12 | Low | Both shipped copies, the schema and the guide change in 4b. The F003 offer IDs are kept. |
| QA-1 | High | Step 15 is a catalog-derived expectations audit. Journeys assert shape. Full Play Mode and player runs follow after 4b and after tuning. |
| QA-2 | High | Quiet host, median of 5 with spread, an early W005-L timing after step 10, and a Performance review task on a miss. A pre-F005 W005-L baseline is impossible, because the fixture needs V3 state. The W003-B record (step 0) and the step-10 timing serve as baselines. |
| QA-3 | High | The session-log writer takes an injected root, prefs and file layer. A test composition seam exists in the test player. |
| QA-4 | High | `BalanceFull` is a named step with ≤ 20 min, and 3b comes before any task that needs it. The fast gate is in the default run. |
| QA-5…QA-14 | Medium/Low | Added to the test strategy as additional cases, to be named per unit in tasks. QA-11 and QA-12 tables are required in tasks. |
| QA-15 | Low | Negative test with an injected unknown code. |
| QA-16 | Low | The API baseline is committed first in 1a. |
| GD splits | — | Applied: 1a–1c, 4a–4c, 6a–6c, 7a–7c, 10a–10b. |

The Architect recommended a re-review of the amended F5-R05. It is scheduled with wave B.

## Wave B (2026-10-10)

| Lens | Model | Verdict |
|---|---|---|
| Software Architect (re-review of `0d1b932`) | sonnet, high reasoning | APPROVE-WITH-CHANGES. AR-1 to AR-16 are resolved. Open: 2 Medium, 5 Low (AR2-1 to AR2-7). No further architect re-review is needed. |
| Game Systems/Economy | sonnet | APPROVE-WITH-CHANGES. 4 High (ECO-1, ECO-3, ECO-4, ECO-5), 10 Medium/Low. |
| UI/UX Reviewer | sonnet | APPROVE-WITH-CHANGES. 1 High (UX-6 placement), 12 Medium/Low. |

Owner decisions on 2026-10-10 from this wave, recorded in spec › Clarifications:

- **D-J**: starting Cash grows per prestige (ECO-5).
- **D-K**: the threshold stops rising at `maxIndex`, and prestige continues (ECO-8).
- **D-L**: auto-sell reserves stock for every active delivery contract (ECO-12).

### Dispositions

All findings are accepted. The consolidated changes are in research F5-R17, with edits in the spec, plan, data model and contracts.

| ID | Disposition |
|---|---|
| AR2-1 | 7a is split. 7a-1 is the mapping and signature change, bit-identical against the W003 hashes. 7a-2 is the delivery behaviour. |
| AR2-2 | Step 6 now runs after 5. Each unit names the goldens it regenerates. |
| AR2-3 | `windowEndUtcMs` is required for both bonus states. A test proves that a lapsed trigger never re-triggers. |
| AR2-4 | The reserve stays while delivery is blocked. The snapshot shows the blocked state, and the event fires only on a state change. |
| AR2-5 | The mapper rename is optional and, if done, a separate mechanical commit. |
| AR2-6 | Accelerated saves are marked development-only in the overlay and the log. |
| AR2-7 | Step 11 now depends on 7a-1. |
| ECO-1 | The production reward bound is R_prod plus the highest standing R ≤ 2.0, and the FR-006 wording is corrected. |
| ECO-2 | Sale modifiers are read at the completion instant (FR-009), with a test. |
| ECO-3 | New quantity cap ≤ top capacity, plus a `contract.capacity` reject at accept (FR-007). |
| ECO-4 | Semantic rules are gated on revision ≥ 3. 4b ships schema v3 with r2 values and a transitional golden. The bands are paused until step 13. |
| ECO-5 | D-J, and the first-Miner check runs with zero discounts and zero Seed Capital. |
| ECO-6 | The build cap is bounded at ≤ 10^12, with a property-test oracle. |
| ECO-7 | The threshold and decay algorithms use iterated floors and are pinned by oracle tests. |
| ECO-8 | D-K. `prestige.maxed` is removed. |
| ECO-9 | The 4.0× rule is a trace gate over the maximum IP across the two prestige strategies (FR-021). |
| ECO-10 | `cost.floor` and `speed.level.dead` are kept as content rules. There is no hard speed or price cap. |
| ECO-11 | Ladder grant rule, production completions count, `contract.produce.min`, `ladder.uptime` defined with `minRunDurationMs`. |
| ECO-12 | D-L, plus a `chain.dominance` definition: monotone efficiency along the chain with a margin at the terminal product. |
| ECO-13 | Stop-at-Bar, stop-at-Plate and market-only chain strategies are added, the single-lever set is defined, and income-share orientation is reported. |
| ECO-14 | Offsets shift the look grid only. Medians are taken per run index, ECO-3 has a tolerance of one look interval, the casual constant is pinned, and the runtime estimate comes first. |
| ECO-15 | The expiry boundary is deadline + 1 tick, with exact tests. |
| ECO-16 | The cooldown clock-jump risk is recorded (Assumptions). |
| ECO r3 notes | Greedy marginal-gain Industry spending in policy r2. The tuning record covers the objective, a fluid model, circularity, Seed Capital and the bound re-derivation. |
| UX-1 to UX-5, UX-7 to UX-13 | UI contract additions (names, focus, sell row, auto-deliver feedback, cooldown tick, price recovery, clear confirmation, last export, DevTools accessibility) and an "Invariants for F004" list. Not adopted: the goal line is **not** hidden when no milestones exist, because FR-018 already shows the threshold as the only goal. |
| UX-6 | The session log becomes the fourth `MarketPanelHost` section (`session-log-section`), and F004 relocates it. |
| UX-10 | Step 14 names its test matrix (`ShellRig` worst case at 200 %, touch and pointer, rotation, names, empty and error states). |

## Wave C (2026-10-10)

| Lens | Model | Verdict |
|---|---|---|
| Performance Engineer | haiku | APPROVE-WITH-CHANGES. 8 High (PF-1 to PF-4, PF-7, PF-9, PF-11, PF-15), 8 Medium/Low. |
| Security/Privacy | haiku | CONDITIONS. 7 conditions (SEC-3 to SEC-9, SEC-13, SEC-14), plus low items. |
| DevOps/Operations | haiku | APPROVE-WITH-CHANGES. 4 High (OPS-1 to OPS-4), 3 Medium/Low. |

Owner decisions on 2026-10-10 from this wave:

- **D-M**: Android exports the log by path only, retrieved with `adb pull`. The share action is deferred to F004. The backup exclusion stays mandatory.
- **D-N**: development builds use a separate save root, `IdleFactory-Dev/`.

### Dispositions

All findings are accepted. The changes are consolidated in research F5-R18, with edits in the spec (D-M, D-N, FR-026, FR-031, FR-032, FR-034), plan, quickstart (lease table), data model and contracts.

| ID | Disposition |
|---|---|
| PF-1 | The pass budget is N + 2, fail-closed at the bound, and the realized maximum is recorded. |
| PF-2 | Auto-delivery happens at most once per timestamp, and the O(1) check is measured. |
| PF-3 | W003-B is re-measured after steps 1, 5, 7a-1, 7a-2 and 7b. The step-0 decision gates 5 and 7a-2. |
| PF-4, PF-5 | The acceleration gate is W003-B with auto-deliver at ×600 and Max on Windows. Max is the S_max clamp. The emulator is evidence only. |
| PF-6 | The mapping is called per advance and per stamp, never per completion. |
| PF-7 | Zero allocations in the session wrapper with logging on. Each flush has an allocation and milliseconds budget. The writer is hand-written. |
| PF-8 | The goals cache is keyed on milestone crossing. |
| PF-9 | The quiet window is defined as the timed window, with the process list recorded. |
| PF-10 | Median of run medians, a 10 % spread limit, and a 10 % regression guard. |
| PF-11, PF-12, PF-16 | The W005-L record contains the counts, passes, V3 write time and size, and the regeneration rule after the retune. |
| PF-13 | Windows Mono development is the gate. IL2CPP is a recorded open item. |
| PF-14 | Windows frame time with UI, average and p95, at W005-L and during acceleration. |
| PF-15 | The BalanceFull runtime is projected first. The matrix has 9 strategies, with offsets reduced if needed. |
| SEC-1, SEC-2 | A writer-output test. The path is shown on screen only. |
| SEC-3, SEC-4 | No FileProvider (D-M). The backup exclusion is unconditional, with merged-manifest checks for `allowBackup`, both attributes and the XML. |
| SEC-5, SEC-9 | Quit and prestige bypass the interval. Clear, toggle and flush are serialized. Callbacks catch everything. |
| SEC-6 | The release scan runs per batch on Windows and Android and is IL2CPP-aware. |
| SEC-7 | `liveGapThresholdMs` ≤ 8 h. |
| SEC-8 | `runCompletionTotal` is added to Save V3 (spec FR-034 updated), and a no-re-grant test is added. |
| SEC-10 | Departure invariant test. |
| SEC-11, SEC-12 | Covered, no change. |
| SEC-13 | D-N: separate dev save root. |
| SEC-14 | The shipped F003 build cannot change, so the downgrade risk is documented. A forward-compatibility test refuses version 4 without writing. |
| SEC-15, SEC-16 | Temp-then-rename exports, at most 10 kept. The session id comes from a cryptographic random number generator. |
| OPS-1 | `TimeScalePlayerTests` leave the tagged suite. The fake is used in Play Mode, and a separate development-player timing run is added. |
| OPS-2 | `-Category` is rejected together with `-StandaloneWindowsPlayer`. |
| OPS-3 | `BuildAndroidArm64Release` is added in 3a. |
| OPS-4 | APK extraction, a `global-metadata.dat` scan, the merged manifest via `gradleOut` or `aapt2`, and a `runInBackground: 0` guard. |
| OPS-5 | Resolved by D-M: no Gradle template. Backup XML only. |
| OPS-6 | `git diff ProjectSettings/` review after 12c. |
| OPS-7 | Lease table in the quickstart. |

## Plan review result

All three waves are complete, and every finding has a disposition. The owner decided D-J to D-N during the reviews. The plan is ready for `$speckit-tasks`.

## Post-review owner changes (2026-10-10)

- **D-J revised**: the owner withdrew "starting Cash grows per prestige". Starting Cash stays constant and low.
- **D-O, new mechanic**: manual mining at an ore vein solves the first-Miner problem (ECO-5) instead. Owner choices:
  - the yield is Ore and can be upgraded;
  - a rate cap applies (initially 10 counted activations per second);
  - both clicks and holds count;
  - the vein is in the Factory view in every run.

  The design is in research F5-R19, with spec US10, FR-040 to FR-044 and SC-011, plus tasks T358 and T359. `start.reachable` and `manual.yield.bound` replace `build.start.affordable`.
- **Analysis fixes** (`speckit-analyze` on `289bcdc`): all applied.

  | Finding | Change |
  |---|---|
  | I1 | Transitional reward adapter in T313, removed in T320. |
  | C1 | Play-Tester batch-1 report T357. |
  | F1 | Plan test-strategy row corrected: `TimeScalePlayerTests` moves to the separate development-player run. |
  | F2 | Rule for serial merges into shared files. |
  | U1, U2 | Wording fixes. |
- **Review**: Economy and UI/UX lenses review the new mechanic (see below).
