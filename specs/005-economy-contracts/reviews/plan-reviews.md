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

## Wave B

Pending: Architect re-review of F5-R05 and the amended contracts; Game Systems/Economy; UI/UX.

## Wave C

Pending: Performance; Security/Privacy; DevOps/Operations.
