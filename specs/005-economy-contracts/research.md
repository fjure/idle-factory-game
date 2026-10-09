# Research: Economy and Contracts (Feature 005)

Phase 0 decisions for [plan.md](plan.md). Each entry gives the decision, the rationale and the rejected alternatives. Inputs:

- the spec,
- [reviews/discovery.md](reviews/discovery.md), which covers both review waves,
- the F003 artifacts,
- the code at `a41c53f`.

Numbering continues the F003 convention with the prefix `F5-R`.

## F5-R01: #356 refactor shape and gate

**Decision**: The refactor is behaviour-preserving and lands before any F005 behaviour change.

- **`RunPersistence`** (1,919 lines) becomes a `partial class`:
  - `RunPersistence.cs`: the lifecycle core, the admission table and the state machine, which stays in one piece;
  - `RunPersistence.ColdStart.cs` and `RunPersistence.Transition.cs`;
  - `RunPersistenceTypes.cs`: `RunPersistenceOutcome`, `Status` and `Reasons`;
  - `OfflineReturn.cs`: `OfflineRunReturnReconciler` and `NoCredit`;
  - an internal `SafeRunStore` that wraps the `Safe*` calls.

  Partial files reduce merge conflicts, not coupling. This is accepted as an intentional trade-off (AR-15).
- **`RunSnapshots`** splits into `.Factory`, `.Economy`, `.Contracts`, `.Items` and `RunDerivation.cs`.
- **`RunState` mutators**: the command-path mutators become `internal`, but the list is derived from a compile and is not fixed in advance (AR-8). Every method that `SaveV2Mapper` (Unity assembly) or another restore path calls stays `public`. This covers at least `TryAddMachine`, `TrySetAutoSell`, `Inventory.TryImport`, `PermanentState.TryAddItems`, `TryRestoreInvestment`, `TrySetResearchLevel`, `TryAddEffect`, `TrySetIndustryLevel`, `ContractBoard.TryRestoreActive` and `TrySetCompletionCount`.
- **Test access**: `IdleFactory.Core/AssemblyInfo` gains `InternalsVisibleTo("IdleFactory.PlayModeTests")` next to the existing EditMode entry.

The gate is `PublicApiSnapshotTests`. Its baseline is committed in the first #356 commit, before the split (QA-16). The #356 result must differ from that baseline by visibility only. Later work units regenerate the baseline, and an architect reviews each diff (AR-9). The existing suites must pass unchanged.

## F5-R02: #353 measurement first, then the hot-path decision

**Decision**: Work unit 0, Option 0, runs in its own worktree at `a41c53f`, in parallel with #356 (AR-12). Only the sale-pricing work unit (step 5) needs its decision record.

- **Base**: `a41c53f`.
- **Host**: quiet, with the editor and the emulator closed, and the state of both recorded.
- **Workload**: W003-B on the Windows Mono player, median of 5 over at least 3 runs, with a phase attribution (step scan, `CompletionPass`, run predicate, `AutoSell`, lookups) through Stopwatch accumulators in a development build.
- **Emulator**: W003-B is also measured there, from a development player with the test catalog.
- **Record**: `reviews/w003b-measurement.md`.

Option 1 is an index-based hot loop as an additional API. If the measurement chooses it, the decision is recorded before the work unit that changes selling and auto-sell (F5-R04, F5-R05). It is implemented as an Architect-approved work unit that preserves bit-identical results against the committed W003 fixtures and hashes. Option 3, the steady-state jump, stays out of F005.

**Rationale**: D-H and the Performance condition C2. The settle path changes in F005, so a later measurement would mix causes.

## F5-R03: Content schema version 3

**Decision**:
- **Schema version**: the packaged content moves to `contentSchemaVersion 3` and stays strict. The reader evolves in place to the v3 constant, so it is not deleted and recreated (AR-6). The generic strictness and canonical-form tests are ported, not removed.
- **Dispatcher**: it keeps its existing diagnostic `content.version.unsupported` for any version other than 3. The tests that used 3 as the "unsupported" example switch to 2 or 4.
- **Legacy**: the v1 legacy catalog stays for V1 import. V1 import fixtures run against the v3 catalog (AR-14).
- **Offer IDs**: the shipped v3 content keeps the three F003 offer IDs (`contract.plate`, `contract.gear`, `contract.timed-bar`), so V2 saves keep their contract state (GD-12).
- **One unit for all copies** (GD-11): these change together:
  - the PlayMode copy `Tests/PlayMode/Fixtures/SaveV1/Resources/RunPlayerFixtures/iron-factory-content.json` and its parity tests;
  - `gameplay-content-v2.schema.json`, which becomes `-v3`;
  - `docs/content/json-content-pipeline.md`.
- **Device sets**: the V2 device-set fixtures (`ShippedDeviceSets`, `DeviceSetShippedCatalogTests`) get a frozen copy of the r2 catalog instead of reading the live shipped file (AR-6).
- **Constructor churn**: a test-catalog builder helper (`TestCatalogBuilder`) and optional constructor parameters with legacy defaults absorb the ~21 `ContractOfferDef` and ~17 `MachineDef` call sites.

The member additions are listed in [contracts/gameplay-content-v3.md](contracts/gameplay-content-v3.md). The rules are split by ownership (AR-7, GD-13):

- Step 4 carries only the **structural** rules: shapes, ranges, `sell.steps`, `live-gap.range`, `contract.bonus.fields`, the `contract.ratio.*` ranges, `milestone.order` and `build.base.bound`.
- The semantic rules land with the work unit that owns their function:
  - `contract.quantity.capacity` and `ladder.uptime` with the contracts unit;
  - `build.price.cap` and `build.start.affordable` with build pricing;
  - `prestige.threshold.bound` and `industry.ceiling.level` with threshold and Industry;
  - `chain.dominance` with sale pricing.

The Industry 4.0× rule is a balance-trace gate (F5-R13).

**Rationale**: The strict v2 reader cannot carry optional members, and the contract vocabulary changes shape.

**Alternatives**:
- Optional members in v2: rejected, because it weakens strictness.
- Deriving the objective from the old `kind` labels: rejected, because it hard-codes three offers.

## F5-R04: One sale price

**Decision**: A pure `SalePricing.Value(units, basePrice, salePricePpm)` in **Domain** (GD-2) returns `MulDivFloor(units × base, salePricePpm, 10^6)` with a checked `units × base`. It is the only sale arithmetic, and three callers share it:

- `RunSimulator.AutoSell`: one sale per resource per instant, after the instant's expiries;
- the new `RunCommands.Sell`;
- `FactoryValuation`: stock at the effective price.

The `Sell(resourceId, quantity | All)` checks run in this order:
1. lifecycle;
2. resource unlocked, else `resource.locked`;
3. quantity ≥ 1, else `sell.quantity-invalid`;
4. quantity ≤ stock, else `stock.insufficient`;
5. the cash bound, else `value.limit`.

It then runs the settle pre-check (`SettleFits`) with the sold stock subtracted, commits, and settles. `All` takes the stock at the command instant.

**Contract reward (GD-9)**: the Cash reward is `MulDivFloor(SalePricing.Value(required, basePrice, salePricePpm), rewardRatioPpm, 10^6)`. That is one sale-basis floor and then one ratio floor, the same expression for manual and automatic delivery and for production objectives. The UI confirmation for an active delivery-contract shortfall (FR-003) is a presenter step before the command. The command itself takes no confirmation flag.

**Rationale**: Full-stock neutrality and "no sale raises FV" follow directly when valuation and sale share one function and one floor.

## F5-R05: Auto-deliver and production objectives inside the simulator

**Decision**: This section replaces the first plan version after AR-1 to AR-4 and GD-1 to GD-3.

**Timeline mapping (AR-1, GD-1)**: A single `TimelineMapping` value maps simulated ticks to real UTC milliseconds. It has these fields:

- `OriginUtcMs`;
- `OriginTickPhase` (0 ≤ phase < 10,000, the sub-millisecond remainder);
- `RealPerSim` as a rational `num/den`: `1/1` live, and the inverse of the offline effectiveness for an offline segment (`5/4` at 80 %).

`RealUtc(t) = OriginUtcMs + floor((OriginTickPhase + t × num / den) / 10,000)` uses integer checked math and is the only rounding rule.

- `RunSession.TimelineUtcMs`, `OfflineSegmenter` and `RunSimulator.Advance` all take or produce this mapping.
- Live frames carry the phase forward, so a live advance and any live partition give identical real stamps.
- `Advance(activeTicks, mapping, boundaryEvents)` replaces `Advance(activeTicks, startUtcMs, ...)`.

**Per-instant order (AR-2)**: This is restated against the real `ProcessTimestamp`.

1. **Boundary events at this tick**: deadline expiry, and Item and Overdrive ends. The deadline is the only contract boundary event.
2. **Settle fixed-point loop**, repeated until nothing changes:
   1. priority completions, which also add to the production objective counter;
   2. auto-delivery, at most once per advance;
   3. auto-sell, keeping the reserve.

   The pass bound is the existing OD-2 bound (machines + 1) plus 1 for the delivery.
3. **Unlock pass (4b)**: unlock conditions, plus the bonus-offer trigger.

**Cooldown and bonus windows (AR-3)**: These are **not** boundary events. They change no simulated state. Cooldown ready times and bonus window ends are stored UTC stamps:

- the cooldown starts at `RealUtc(t_end)` + cooldown;
- the bonus window starts at `RealUtc(t_trigger)` + window.

Availability is evaluated lazily against the session timeline when a snapshot or an `AcceptContract` command reads it. A bonus trigger inside an advance only records `Triggered` and the window end. It never inserts events.

**Deadline race (AR-4)**: The simulation order is authoritative.

- A delivery or production completion at a tick before the deadline boundary event succeeds, even if `RealUtc` of that tick rounds to a later millisecond.
- The simulator never compares real time for delivery.
- The scratch contract state ensures a contract is either completed or expired, never both.

**Scratch and commit (GD-3)**: The simulator keeps scratch copies and commits them together at the end of the advance, as today:

- `_stock` and `_cash`;
- new `_rp`;
- scratch contract state: active, produced, completion index, ladder grants, cooldown entry, bonus states, `LastOutcome`;
- a scratch Item-grant list, which is applied to `_permanent` at commit.

**Delivery failure (AR-4)**: An overflow (`value.limit`) blocks the auto-delivery. Nothing is consumed or granted, the contract stays active with the flag on, and the outcome records `AutoDeliverBlocked(value.limit)`. Each instant retries at O(1) cost until the deadline. A ladder Item beyond the per-run cap is simply not granted, which is not a failure.

**Shared arithmetic (GD-2)**: `SalePricing` and `ContractRewards` are pure static classes in **Domain** (`IdleFactory.Run.Domain`), so Simulation, Domain and Application can all call them without inverting the layering. `RunState.TryDeliverContract` moves onto `ContractRewards`.

**Commands**: `SetAutoDeliver(true)` pre-checks the worst case with the same pre-check as `DeliverContract` (`ContractRewards.CheckGrant` plus `SettleFits(cash + reward, consumed resource, required)`). The following settle delivers at t = 0 if the stock is present. A test pins this (AR-2).

**Revisions (GD-7)**: `ContractBoard` gets a `Version` counter. It is incremented on every contract-state change (accept, produced, flag, cooldown, bonus, outcome) and included in `RunSession.Publish` tokens, so presenters refresh.

**Session events (AR-13, AR-14)**: The session raises simulator outcomes (auto-delivery, production completion, expiry, blocked) as `IRunEventSink` events from a before/after comparison of `LastOutcome` and the board version after each advance. The simulator itself never calls the sink. The offline summary gains `AutoDelivered`, `ProduceCompleted`, `Expired` and the reward totals.

**Rationale**: Segment invariance (FR-012, SC-003) needs evaluation at exact completion instants, plus one mapping with one rounding rule.

**Alternatives**:
- A real-time compare for the deadline: rejected because of rounding races.
- Boundary events for cooldown and bonus: rejected, because they would change no state and could not be inserted mid-advance.

## F5-R06: Build pricing

**Decision**: The algorithm is the spec. A BigInteger oracle in the tests uses the same steps (GD-5).

```text
if base > 10^12 → content rejects (build.base.bound)
acc = base × 10^6
repeat owned times:          acc = MulDivFloor(acc, ownedGrowthPpm, 10^6);   if acc ≥ cap × 10^6 → return cap
repeat prestigeCount times:  acc = MulDivFloor(acc, prestigeGrowthPpm, 10^6); if acc ≥ cap × 10^6 → return cap
price = ceil(acc / 10^6); return min(price, cap)
```

- Every factor is at least 10^6, so `acc` never shrinks, and the early cap return keeps it at most `cap × 10^6 × max growth`. That stays far below 10^18 for `cap ≤ 10^12`. A capped price therefore stays buildable.
- At most 76 steps are needed (12 owned, 64 prestiges), and the prestige factor is precomputed per run.
- The F003 discounts then apply with the round-up rule.
- `Build(slot, machineId, long? expectedPrice = null)` (GD-6): `null` means the price at the call instant, so the ~92 test call sites stay unchanged. The UI always passes the displayed price. A mismatch rejects with `build.price.changed`.
- Snapshots cache the price per structure revision.

## F5-R07: Rising threshold and milestones

**Decision**:
- **Threshold**: `PrestigeThreshold.For(n) = ceil(base × growthPpm^n / 10^6^n)` with checked math, where `n = PermanentState.PrestigeCount`, which is already persisted.
- **Preview and confirm** read `For(PrestigeCount)`. If `PrestigeCount` reaches `maxIndex`, prestige rejects with `prestige.maxed`.
- **IP** keeps the F003 formula with the fixed divisor. The candidate carries the incremented permanent state, so the existing durable protocol (`EveryProtocolStepFailingOnce`) already makes index and IP exactly-once. A new test proves that a retry does not double the index.
- **Milestones**: values are `floor(threshold × fractionPpm / 10^6)`, derived per run. A new `GoalsSnapshot` holds the next goal, the amount, the percentage (floored) and the reached flag.
- **Notices**: the one-time notice is presentation state keyed by `(runId, milestoneIndex)`. It is not persisted: after a load, already-passed milestones do not re-notify, because the presenter seeds them from the current Factory Value.

## F5-R08: Industry with diminishing returns

**Decision**: Decay applies per effect kind and keeps the `FoldLevels` floor per level (GD-4).

- **Multiplicative kinds** (`machineSpeed`, `salePrice`): level L adds the increment `floor((v − 10^6) × decayPpm^(L−1) / 10^6^(L−1))` to 10^6. The fold multiplies the per-level factors as today.
- **Discount kind** (`upgradeCost`, v < 10^6): level L uses the decrement `floor((10^6 − v) × decay^(L−1))`, and the factor is `10^6 − decrement`.
- **Additive kinds** (`startingCash`, `startingStorageLevel`): level L adds `floor(amount × decay^(L−1))`.

A cumulative fold table per upgrade is precomputed at catalog build up to `maxLevel`, so the hot path is a table lookup.

The practical ceiling is the first level whose marginal effect is below `minGainPpm` of the base (`industry.ceiling.level`). The practical Industry ceiling is the summed IP cost to reach those levels. The 4.0× rule is asserted in `BalanceTraceTests` against policy r2.

## F5-R09: Save V3

**Decision**:
- **One mapper** (GD-10, AR-5): `SaveV2Mapper` stays a single mapper and becomes version-parametrized (renamed in place to `SaveMapper`). Its per-version member tables are selected at the `schemaVersion` peek. There are no copied DTOs. `ValidateBytes` compares against the version actually read.
- **Writer**: always writes V3, and only after a successful load or a fresh start.
- **Codes**: the existing `save.v2.*` diagnostic codes stay.
- **Storage**: the `Checkpoints/v2/` directory and slot names stay, and V3 files live there. Downgrade is dev-only and documented.
- **V3 members**: `activeContract.autoDeliver`, `activeContract.produced`, `contractCooldowns[]` and `bonusOffers[]`, as in [contracts/save-v3.md](contracts/save-v3.md). This list is closed.
- **Restore setters**: `ContractBoard` gains `TryRestoreCooldown`, `TryRestoreBonus` and `TryRestoreProduced`. They are public restore paths and enter the API baseline.
- **Loading a V2 file**: auto-deliver false, produced 0, no cooldowns, no bonus state.
- **Range checks**: booleans are strict, and numbers must lie in [0, 10^18]. A cooldown ready time at or before the load's timeline start loads as ready.
- **Content drift**: dropping is restricted to contract-scoped lists. An `activeContract`, `contractCooldowns` entry, `bonusOffers` entry or `completionCounts` entry whose offer ID is unknown in the loaded content is dropped. An active contract whose offer changed objective is also dropped. Each drop is reported as `contract.dropped` in the existing load summary. Every other unresolved ID (machines, resources, Items, research, Industry) stays fail-closed (`checkpoint.content`).
- **Durability**: generation, promotion and the durability protocol are unchanged. A failed V3 write keeps the previous generation, which a fault-injection test proves after a V2 load (QA-11).

## F5-R10: Large live gap

**Decision**:
- **Clock read** (AR-11): `FactoryRuntimeHost` gets an explicit UTC read through the existing clock port. Today it reads only a cumulative-active-seconds delegate.
- **Gap rule**: a wall delta strictly greater than `runStart.liveGapThresholdMs` triggers `RunPersistence.RecordSuspension(atUtcMs: previousFrameUtcMs)`, a new overload of the existing path. It goes through `Admit` and the lifecycle table (Running → Checkpointing → Suspended, including `SuspendWithPendingTransition`) and is followed by `ResumeAndReconcile`.
- **Departure clamp**: `max(currentDeparture, min(atUtcMs, timelineEnd))`.
- **Other deltas**: a negative delta credits nothing, and a gap equal to the threshold is simulated live.
- **Accelerated timeline ahead of the wall clock** (development builds only): the departure rule makes the next offline credit 0 (D-I), and the session log flags it.
- **Time scale**: it multiplies only the simulated delta after the gap check.
- **#354**: `Autosave` gains a minimum interval guard that reads the same content value. #354 also adds the failing-write adapter test and the quit-then-focus-lost test.
- **Test matrix** (QA-10):
  - threshold −1, threshold and threshold +1 ms;
  - a negative clock;
  - accelerated frames not counting;
  - a checkpoint inside a gap;
  - a gap during a pending prestige.

## F5-R11: Development time acceleration

**Decision**:
- **Assembly**: a new assembly `IdleFactory.DevTools` with `defineConstraints: ["IDLEFACTORY_DEV"]`, referencing `IdleFactory.Unity` and never referenced back.
- **Seam**: `IdleFactory.Unity` exposes a null-default `ITimeScaleSource` seam. DevTools registers it in `RuntimeInitializeOnLoadMethod` and adds the overlay control (1, 10, 60, 600, Max), the permanent "ACCELERATED ×N" marker, and the log header and event flag.
- **Clamp**: the host clamps the scaled delta to S_max = 10 simulated seconds per frame.
- **Define handling** (AR-10): the define is applied per build through `BuildPlayerOptions.extraScriptingDefines` in new development variants of `PlatformBuildCommands`. It is never committed to ProjectSettings, so it adds no settings noise.
- **Tests**:
  - The host clamp and the seam are tested in the normal Play Mode suite through an `ITimeScaleSource` fake inside `IdleFactory.Unity` test code.
  - DevTools UI tests live in a separate test assembly with the same `defineConstraints`, so the normal suites compile without the define.
  - `TimeScalePlayerTests` run in a development player built with the define.
- **Release proof**: `scripts/check-release-build.ps1` scans a non-development build for the DevTools assembly and type names.

**Alternatives**: plain `DEVELOPMENT_BUILD` gating was rejected, because test and profiling players are development builds.

## F5-R12: Session log

**Decision**:
- **Core**:
  - a port `IRunEventSink` with a no-op default, and readonly allowlisted event structs;
  - added right after #356 as its own small unit (AR-13), so every later unit emits its own events;
  - events come from commands, persistence outcomes, the offline summary and the prestige transition, and simulator outcomes come through the session's `LastOutcome` comparison (F5-R05);
  - the simulator never calls the sink.
- **Samples**: the host takes them at timeline minutes 10, 15, 30, 60, 90 and 120 and at milestones. Bottleneck seconds come from the simulator's per-machine wait counters, exposed as derived totals at sample time.
- **Unity adapter `SessionLogWriter`**:
  - **Buffer and flush**: an in-memory ring buffer, JSON lines with CRC32, and a flush at lifecycle points and at most every 30 s, off the frame path.
  - **Segments**: two rotating ~128 KB segments under `persistentDataPath/IdleFactory/SessionLog/`.
  - **Failure handling**: try/catch around every call and a drop counter.
- **Test isolation** (QA-3): the writer takes an injected root directory, an injected `ISessionLogPrefs` (the on/off setting) and an injected file layer. Tests use a temp root and a failing file layer. The player failure test injects the failing layer through a test composition seam that is compiled into the test player.
- **Setting**: `PlayerPrefs` (per device), default on.
- **Export**:
  - It writes `Exports/session-log-<utc>.jsonl` and shows the path.
  - On Android it also offers a share intent through a `FileProvider`, with backup rules excluding `SessionLog/` and `Exports/` (work unit 12c). The plan review may reduce this to path-only plus `adb pull` evidence.
- **Release gate**: `scripts/check-release-build.ps1` checks the Android merged manifest for the backup exclusion.
- **Session id**: 128 random bits per app start.

## F5-R13: Reference policy r2, strategies and trace gates

**Decision**: `ReferencePolicy` r2 is written by Game Systems in `reviews/balance-policy-r2.md` and implemented in `Tests/EditMode/Balance/`. It fixes these points (FR-022):

- income measured as net-worth growth;
- storage bought on `StorageFull` blockers;
- a parameterised Industry order;
- a balanced mix derived from recipe rates;
- use of `Sell` and auto-deliver.

Strategy policies (FR-023) share one `IStrategyPolicy` interface: Ore-only, contracts-only, chain, idle-heavy, casual, and prestige-later.

- `BalanceTraceRunner` runs runs 1–5 for each strategy over 5 look-phase offsets and reports the median and the spread (`trace-r3.csv`, golden, regenerated on opt-in).
- The asserted gates (FR-024) live in `BalanceTraceTests`.
- **Full matrix** (QA-4):
  - It is `[Category("BalanceFull")]`, excluded from the default run, with a runtime budget of ≤ 20 minutes.
  - It is a named, recorded step once per gameplay batch and before content approval.
  - The `-Category` runner parameter (step 3b) lands before any task whose acceptance requires it. Until then, the matrix runs with `-TestFile`.
- **Fast gate**: it stays in the default Edit Mode run, with the chain and Ore-only strategies at one offset, runs 1–2, and the static content rules.
- The non-rounding price variant replaces `var.price-80`.

## F5-R14: Performance workloads and budgets

| Item | Definition |
|---|---|
| Measurement conditions (QA-2) | A quiet host: no editor and no emulator running, even while the leases are held. Median of 5 with the spread recorded, never a single run. The W005-L fixture and a first measurement come right after Save V3 lands (step 10) as an early warning, long before the final evidence. A miss opens a Performance review task with options. |
| W005-L (gate, D-H) | A versioned Save V3 fixture from the policy-r2 trace near the run-3 threshold. It has 12 slots, the chain mix, auto-deliver active on a delivery contract and auto-sell on the end products. Its hash is recorded with every measurement. Budgets: 8 h return ≤ 500 ms on the Windows player (median of 5) and ≤ 2 s on the emulator (median of 3); 60 fps on both during play. |
| W003-B (bound) | Measured on `a41c53f` (F5-R02), then again after #356 and after F5-R05, each time with auto-deliver active as a variant. Recorded, not gated. |
| Acceleration | Host frame simulation p95 ≤ 1 ms at S_max on Windows for W005-L. Measured at ×1, ×10, ×60, ×600 and Max. |
| Log | 0 `GC.Alloc` samples per warmed `Advance` with logging on, and at most one flush per 30 s. |
| Snapshots | Build price and goals are cached per structure/FV revision. The snapshot rebuild count stays at or below the structural changes. |

## F5-R15: UI hand-off and functional controls

**Decision**: F005 extends the existing panels and follows `run-panel-conventions`:

- the Market › Inventory row gets a step selector and a Sell button;
- the active contract card gets an auto-deliver toggle (delivery objectives only) and the remaining cooldown;
- the Contracts tab gets a text badge;
- the build sheet shows the next price and the owned count;
- Market gets a goal line at the top;
- a minimal "Session log" section goes in the existing settings/notice area, as the plan review decides;
- the DevTools overlay carries the acceleration control.

[contracts/ui-surfaces-f005.md](contracts/ui-surfaces-f005.md) lists stable element names, commands and reason codes as the F004 hand-off.

## F5-R16: Reason-code contract pin (#355)

**Decision**:
- **Stable location** (AR-16): the stable reason-code table moves to a feature-independent path, `docs/contracts/run-reason-codes.md`. Feature contracts link to it.
- **Pin test**: `ReasonCodeContractTests` reflects over `RunReasons` and the persistence reason classes, parses that table and asserts set equality. It also asserts that `RunRejectionCopy` has copy for every rejection code. A negative test injects an unknown code (QA-15).
- **Codes added**:
  - F003 omissions: `command.invalid`, `checkpoint.content`, `discard.unavailable`;
  - F005: `sell.quantity-invalid`, `build.price.changed`, `contract.cooldown`, `contract.bonus.unavailable`, `prestige.maxed`, and the informational `contract.dropped`.
