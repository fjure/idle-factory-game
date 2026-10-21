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
  - `build.price.cap` with build pricing, and `start.reachable` and `manual.yield.bound` with manual mining (F5-R19);
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
- **Preview and confirm** read `For(min(PrestigeCount, maxIndex))`. Beyond `maxIndex` the threshold stays constant and prestige continues (D-K). There is no `prestige.maxed` code.
- **IP** keeps the F003 formula with the fixed divisor. The candidate carries the incremented permanent state, so the existing durable protocol (`EveryProtocolStepFailingOnce`) already makes index and IP exactly-once. A new test proves that a retry does not double the index.
- **Milestones**: values are `floor(threshold × fractionPpm / 10^6)`, derived per run. A new `GoalsSnapshot` holds the next goal, the amount, the percentage (floored) and the reached flag.
- **Notices**: the one-time notice is presentation state keyed by `(runId, milestoneIndex)`. It is not persisted: after a load, already-passed milestones do not re-notify, because the presenter seeds them from the current Factory Value.

## F5-R08: Industry with diminishing returns

**Decision**: Decay applies per effect kind and keeps the `FoldLevels` floor per level (GD-4).

- **Multiplicative kinds** (`machineSpeed`, `salePrice`): level L adds the increment `floor((v − 10^6) × decayPpm^(L−1) / 10^6^(L−1))` to 10^6. The fold multiplies the per-level factors as today.
- **Discount kind** (`upgradeCost`, v < 10^6): level L uses the decrement `floor((10^6 − v) × decay^(L−1))`, and the factor is `10^6 − decrement`.
- **Additive kinds** (`startingCash`, `startingStorageLevel`): level L adds `floor(amount × decay^(L−1))`.

A cumulative fold table per upgrade is precomputed at catalog build up to `maxLevel`, so the hot path is a table lookup.

The practical ceiling is the last level whose marginal effect is still at least `minGainPpm` of the base, or `maxLevel` if every level qualifies; if even level 1 is below `minGainPpm` the content is rejected (`industry.ceiling.level`; owner decisions 2026-10-13 and 2026-10-13-b, #396). The practical Industry ceiling is the summed IP cost to reach those levels. The 4.0× rule is asserted in `BalanceTraceTests` against policy r2.

## F5-R09: Save V3

**Decision**:
- **One mapper** (GD-10, AR-5): `SaveV2Mapper` stays a single mapper and becomes version-parametrized (an optional rename to `SaveMapper` is done only as a separate, mechanical commit in 10a). Its per-version member tables are selected at the `schemaVersion` peek. There are no copied DTOs. `ValidateBytes` compares against the version actually read.
- **Writer**: always writes V3, and only after a successful load or a fresh start.
- **Codes**: the existing `save.v2.*` diagnostic codes stay.
- **Storage**: the `Checkpoints/v2/` directory and slot names stay, and V3 files live there. Downgrade is dev-only and documented.
- **V3 members**: `activeContract.autoDeliver`, `activeContract.produced`, `contractCooldowns[]`, `bonusOffers[]` and `runCompletionTotal` (F5-R18), as in [contracts/save-v3.md](contracts/save-v3.md). This list is closed.
- **Restore setters**: `ContractBoard` gains `TryRestoreCooldown`, `TryRestoreBonus` and `TryRestoreProduced`. They are public restore paths and enter the API baseline.
- **Loading a V2 file**: auto-deliver false, produced 0, no cooldowns, no bonus state.
- **Range checks**: booleans are strict, and numbers must lie in [0, 10^18]. A cooldown ready time at or before the load's timeline start loads as ready.
- **Content drift**: dropping is restricted to contract-scoped lists. An `activeContract`, `contractCooldowns` entry, `bonusOffers` entry or `completionCounts` entry whose offer ID is unknown in the loaded content is dropped. An active contract whose offer changed objective is also dropped. Each drop is reported as `contract.dropped` in the existing load summary. Every other unresolved ID (machines, resources, Items, research, Industry) stays fail-closed (`checkpoint.content`). Three points are exact (#401, from the #400 review; the normative text is [contracts/save-v3.md](contracts/save-v3.md), Content drift on load):
  - (a) A `run.unlocked` ID of a removed offer is dropped only when a contract-scoped list of the same file (`completionCounts`, `contractCooldowns`, `bonusOffers` or the `activeContract`) names that ID. An unlock ID that no such list names stays fail-closed.
  - (b) An objective change is detected only from V3 evidence (`produced` above 0 means production, `autoDeliver` true means delivery) together with a `contentRevision` that differs from the loaded content's. A change without evidence keeps the contract, and evidence with an equal revision stays a `checkpoint.invalid` invariant failure.
  - (c) An unknown offer ID is drift even when the revision is equal, because the ID alone proves the content changed. A drop never grants anything, so a forged revision gains nothing.
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
  - It writes `Exports/session-log-<utc>.jsonl` and shows the path on every platform (D-M). On Android, evidence uses `adb pull`. There is no share intent, no `FileProvider` and no Gradle template in F005.
  - Android backup rules exclude `SessionLog/` and `Exports/` (work unit 12c).
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
- the "Session log" controls go in a fourth `MarketPanelHost` section (`session-log-section`), after Inventory, Priorities and Expansion, because the shell has no settings surface (UX-6). F004 relocates it;
- the DevTools overlay carries the acceleration control.

[contracts/ui-surfaces-f005.md](contracts/ui-surfaces-f005.md) lists stable element names, commands and reason codes as the F004 hand-off.

## F5-R16: Reason-code contract pin (#355)

**Decision**:
- **Stable location** (AR-16): the stable reason-code table moves to a feature-independent path, `docs/contracts/run-reason-codes.md`. Feature contracts link to it.
- **Pin test**: `ReasonCodeContractTests` reflects over `RunReasons` and the persistence reason classes, parses that table and asserts set equality. It also asserts that `RunRejectionCopy` has copy for every rejection code. A negative test injects an unknown code (QA-15).
- **Codes added**:
  - F003 omissions: `command.invalid`, `checkpoint.content`, `discard.unavailable`;
  - F005: `sell.quantity-invalid`, `build.price.changed`, `contract.cooldown`, `contract.bonus.unavailable`, `contract.capacity`, and the informational `contract.dropped`.

## F5-R17: Plan review wave B amendments

These amendments come from the Architect re-review (AR2), the Economy review (ECO) and the UI/UX review (UX), with owner decisions D-J, D-K and D-L. Where earlier sections disagree, this section wins.

- **Reserve (D-L, ECO-12)**: while any delivery contract is active, auto-sell keeps `min(stock, required)` of its resource, whether or not auto-deliver is on. While auto-delivery is blocked by `value.limit`, the reserve stays, the snapshot shows the blocked state, and `AutoDeliverBlocked` is emitted only on state change (AR2-4).
- **Production reward bound (ECO-1)**: `contract.ratio.produce` requires 0 < R_prod and R_prod + max standing delivery R for the same resource ≤ `maxRewardRatioPpm` (2.0). The stacked payout from producing and then delivering the same units stays within the cap.
- **Modifier instant (ECO-2)**: sale-price modifiers are read at the completion instant. There is a test with Research bought between accept and delivery.
- **Quantity cap (ECO-3)**: delivery offers get `quantity.cap` ≤ the top storage capacity (`contract.quantity.cap`). At accept, `required > current capacity` rejects with the new code `contract.capacity`, and the snapshot shows it as a soft-disabled reason.
- **Revision gating (ECO-4)**: the semantic rules (`contract.ratio.standing`, `contract.ratio.produce`, `chain.dominance`, `ladder.uptime`, `start.reachable`, `manual.yield.bound`) apply only to `contentRevision ≥ 3`. Step 4b ships schema v3 with revision 2 values. Where the formula changes outputs (ratio rewards instead of fixed-dollar curves), step 4b regenerates `trace-r2.csv` once as a labelled **transitional golden**. The conservation, SC-001 and engine gates stay asserted. The F003 pacing bands are paused (reported, not asserted) until step 13, and every unit states which goldens it regenerates (AR2-2).
- **Starting Cash (D-J, revised)**: Starting Cash stays constant. The first-Miner check is `start.reachable` (F5-R19), evaluated with **zero discounts and zero Seed Capital** at every prestige index up to `maxIndex`.
- **Bounds (ECO-6)**: `build.base.bound` covers `base ≤ 10^12` and `cap ≤ 10^12`. A random-parameter property test checks `BuildPricing` against the BigInteger oracle.
- **Algorithms (ECO-7)**: the threshold uses `acc = base × 10^6`, then per prestige step `acc = MulDivFloor(acc, growthPpm, 10^6)`, then `ceil(acc / 10^6)`, pinned by an oracle test. The Industry decay `decay^(L−1)` uses iterated floors in the same way.
- **Threshold clamp (D-K, ECO-8)**: the index is clamped at `maxIndex`. `prestige.threshold.bound` checks T(maxIndex).
- **4.0× rule (ECO-9)**: this is a trace gate only, evaluated against the maximum IP over runs 1–4 across the at-threshold and prestige-later strategies.
- **Safeguards (ECO-10)**: content validation keeps a combined build and upgrade cost floor (`cost.floor`, initial 0.5×) and `speed.level.dead` (no purchasable level with zero effect after floors). Speed and sale price have no hard cap, per D-C. The F003 overflow bounds (`maxCombinedSpeedPpm`, `maxCombinedOutputFactor`) remain.
- **Ladder (ECO-11)**: the grant rule is that a ladder entry is granted when the pre-increment run-wide completion index equals the entry's index, up to `perRunCap`. Production completions count, and production objectives have a minimum quantity rule (`contract.produce.min`) so that trivial offers cannot farm early Items. `ladder.uptime` uses a content constant `minRunDurationMs`: for each Item, `perRunCap × duration ≤ uptimeMaxPpm × minRunDurationMs`.
- **Chain dominance (ECO-12)**: `E(P) = price(P) / Σ slot-seconds per unit over a balanced mix`. The rule requires `E` to be monotone along the chain and `E(terminal) > E(Ore) × (1 + margin)`. Step 16 records the likely price or recipe moves.
- **Strategies (ECO-13)**: stop-at-Bar, stop-at-Plate and market-only chain are added. The single-lever set is Ore-only, contracts-only, stop-at-Bar and stop-at-Plate. Contract income share and the per-delivery reward ratio are reported as orientation.
- **Ensemble (ECO-14)**: the offsets shift only the look grid (0, 4, 8, 12, 16 s), not the idle windows. Histories are independent per offset, and medians are taken per run index. ECO-3 allows a tolerance of one look interval. The casual calibration is a pinned constant. Before the matrix size is committed, its runtime is estimated from the r2 run time, and offsets or strategies are reduced if the 20-minute budget would be exceeded.
- **Policy r2 Industry (ECO r3 notes)**: IP is spent greedily by marginal gain per IP, not in a static order. The r3 tuning record states the objective and order, pre-solves with a closed-form fluid model before the trace, notes the threshold → IP → 4.0× circularity, replaces the placeholder Seed Capital, and documents how the `contracts-off` and `returns` bounds are re-derived.
- **Expiry boundary (ECO-15)**: the expiry boundary event is at deadline + 1 ms (one timeline millisecond, as in F003 R05; owner decision 2026-10-15, #391). Exact-boundary tests cover the deadline millisecond and deadline + 1 ms.
- **Clock risk (ECO-16)**: forward clock jumps also shorten cooldowns and bonus windows. This is recorded in the risk list, and the session log records clock events.
- **Bonus persistence (AR2-3)**: `windowEndUtcMs` is required for both `triggered` and `consumed`. A lapsed `triggered` entry never re-triggers, also after a reload, which a test pins.
- **Accelerated saves (AR2-6)**: development only. The DevTools overlay states that saves from accelerated sessions are not comparable. The log header marks them.
- **UI (UX-1 to UX-13)**: the controls get these additions:
  - an accessible-name pattern per control;
  - a per-row focus order (step selector → Sell → Auto-sell), with focus staying on soft-disabled controls and returning to the invoker after dialogs;
  - the session-log section in Market;
  - a Contracts badge as short text (`Contracts (n)` or `Contracts !`, falling back to `•` on compact), which clears when nothing is deliverable;
  - an auto-deliver notice, a short-stock expiry line and a re-accept reset hint;
  - a cooldown/bonus once-per-second tick and the window-end transition;
  - `build.price.changed` recovery copy, and the rebuild price in the Remove confirmation;
  - a confirmation for clearing the log;
  - a persistent "Last export" line;
  - DevTools marker accessibility;
  - an "Invariants for F004" list.

  The goal line stays Market-only, recorded as a known limitation for F004.

## F5-R18: Plan review wave C amendments

These amendments come from the Performance (PF), Security (SEC) and DevOps (OPS) reviews, with owner decisions D-M and D-N. Where earlier sections disagree, this section wins.

- **Pass bound (PF-1)**: the W003 pass budget becomes N + 2. At the bound the advance fails closed with `value.limit`-style safety: no partial commit, a diagnostic, and a test. The realized maximum passes per timestamp is recorded for W003-B with auto-deliver.
- **Once per timestamp (PF-2)**: auto-delivery happens at most once per **timestamp**. A delivered contract clears, so at most one delivery per contract instance follows anyway. The per-pass check is O(1) and its cost is measured.
- **Re-measurement points (PF-3)**: W003-B is re-measured with and without auto-deliver after steps 1, 5, 7a-1, 7a-2 and 7b, always on the same hashes. The step-0 decision gates 5 and 7a-2.
- **Acceleration gate (PF-4, PF-5)**: W003-B with auto-deliver at ×600 and at Max on Windows. Max is defined as the S_max clamp, and the effective multiplier is reported. Emulator acceleration timing is evidence only.
- **TimelineMapping cost (PF-6)**: the mapping is called per advance and per stamp, never per completion. The legacy overload stays off the hot path.
- **Allocations and flush (PF-7)**:
  - The zero-allocation rule extends to the session wrapper with logging on.
  - Each flush has an allocation and maximum-milliseconds budget on Windows and Android.
  - The writer is hand-written, with no reflection JSON (IL2CPP).
  - Events are emitted per contract instance, never per timestamp.
- **Caches (PF-8)**: the goals cache is keyed on milestone crossing, not on every FV change. FV-driven text updates use hot reads, as F003 HUD values do.
- **Quiet host (PF-9)**: during the timed window of a Windows run, no Unity editor or emulator process may run. The batch editor that builds the player exits first. The process list and the time window are recorded. The emulator run's host condition is the emulator alone, with no editor.
- **Statistics (PF-10)**: median of 5 per run, the decision on the median of run medians, a spread limit of 10 %, and a relative regression guard of 10 % on W003-B with auto-deliver against the post-#356 baseline.
- **W005-L record (PF-11, PF-12, PF-16)**:
  - completions, timestamps and passes per timestamp;
  - Save V3 write time and size;
  - after the step 16 retune, the fixture is regenerated, and both hashes and both timings are recorded.
- **Platform (PF-13)**: the gate runs on the Windows Mono development player. IL2CPP is a recorded open item, and the emulator (IL2CPP) is evidence.
- **Frame time (PF-14)**: Windows frame time, average and p95, with UI over 600 frames at W005-L and during acceleration.
- **BalanceFull runtime (PF-15)**: the r2 runtime per strategy and offset is recorded first, the 9-strategy matrix is projected, and offsets are reduced if the 20-minute budget is exceeded. Scale/Scope and the quickstart reflect 9 strategies.
- **Writer output (SEC-1)**: a test asserts that every string the writer emits is a content ID or an enum token.
- **Path display (SEC-2)**: the "Last export" path is on screen only. It never goes into the export file, the log header, a lifecycle record or `PlayerPrefs`.
- **Android export and backup (SEC-3, SEC-4, D-M)**: no FileProvider. The backup exclusion is unconditional. The release check verifies in the merged manifest that `allowBackup` is not disabled in a way that bypasses the rules, that both `fullBackupContent` and `dataExtractionRules` attributes are present, and that the XML excludes `SessionLog/` and `Exports/`.
- **Interval bypass and serialization (SEC-5, SEC-9)**: quit and prestige checkpoints bypass the minimum interval. Clear, toggle and flush are serialized. Both behaviours are tested. Lifecycle callbacks catch every exception.
- **Release scan (SEC-6, OPS-3, OPS-4)**: `check-release-build.ps1` runs once per batch on a Windows non-development build and on an Android release APK (`BuildAndroidArm64Release`, new in 3a, unsigned for the scan).
  - On Windows it checks the `Managed/` assembly list (Mono).
  - On Android it extracts the APK with `System.IO.Compression` and scans `global-metadata.dat` for DevTools assembly and type names.
  - It reads the merged manifest from `Temp/gradleOut` or, as a fallback, through `aapt2 dump xmltree` with the SDK path from configuration.
  - It asserts `runInBackground: 0` in ProjectSettings as a guard.
  - The exact paths are verified once after a real build.
- **Live-gap bound (SEC-7)**: `live-gap.range` requires 1,000 ms ≤ `liveGapThresholdMs` ≤ 8 h.
- **Ladder total under drift (SEC-8)**: dropping completion counts of removed offers must not lower the run-wide completion index. Save V3 adds `runCompletionTotal` (long) to the closed member list. A V2 load derives it from the sum of completion counts before any drop. A test proves no Item is re-granted.
- **Departure invariant (SEC-10)**: an invariant test proves that the departure from `RecordSuspension(atUtcMs)` is never later than the current UTC read and that only the gap path calls the overload.
- **Dev save root (SEC-13, D-N)**: builds with `IDLEFACTORY_DEV` use `persistentDataPath/IdleFactory-Dev/` for checkpoints, the session log and exports. A test in the DevTools test assembly checks the root.
- **F003 build reading V3 (SEC-14)**: a shipped F003 build cannot be changed. A downgrade onto V3 files is a documented development-only risk, as with OD-7 in the release note. F005 adds a forward-compatibility test: a save with `schemaVersion` 4 is refused with `checkpoint.version`, without any write and without V1 import.
- **Export files (SEC-15)**: written to a temp file and then renamed. At most 10 export files are kept, and the oldest is removed.
- **Session id (SEC-16)**: generated with `System.Security.Cryptography.RandomNumberGenerator`, never derived from device data.
- **Dev-player tests (OPS-1)**: `TimeScalePlayerTests` are not part of the tagged StandalonePlayer suite. The clamp and the seam are tested in Play Mode with the fake. Acceleration timing runs in a separate development-player run, built through the `PlatformBuildCommands` dev variant with `IDLEFACTORY_DEV`, holding the unity and emulator leases.
- **`-Category` (OPS-2)**: `test-unity.ps1` rejects `-Category` together with `-StandaloneWindowsPlayer`.
- **Settings diff (OPS-6)**: after the step 12c build, `git diff ProjectSettings/` must be empty except for deliberate changes. The noise check is not claimed to cover Android manifest or template changes.
- **Leases per step (OPS-7)**: these are listed in the quickstart.

## F5-R19: Manual mining (D-O) and constant starting Cash (D-J revised)

**Decision**:
- **Command**: `RunCommands.Mine(activations)` in `RunCommands.Mining.cs`. It runs at the command's timeline instant like every command, with this order:
  1. lifecycle;
  2. rate cap;
  3. capacity (`storage.full`, new code), where the yield must fit or the activation is rejected;
  4. `value.limit`;
  5. one checked add of `activations × effectiveYield` Ore, then settle.
- **Rate cap**: a Core counter `(timelineSecond, countedInSecond)` on `RunState` that is transient and not persisted. After a load the counter starts fresh, which grants at most one extra second of the cap. Activations above `maxActivationsPerSecond` in the same timeline second are ignored silently (no rejection code, the result reports the counted number). The counter is keyed on run-timeline seconds, so time acceleration in development builds does not raise the real-time rate.
- **Hold**: the Unity presenter accumulates the hold duration on the frame clock and issues `Mine(n)` with `n = floor(heldMs × cap / 1000) − issued`. Release, rotation, focus loss or an open dialog end the hold. Holds and clicks share the Core cap.
- **Yield**:
  - `runStart.manualMining { resourceId, baseYield, maxActivationsPerSecond, startWindowMs, maxShareOfMinerPpm }`.
  - A new effect kind `manualYield` (multiplicative ppm) in the closed effect vocabulary, usable by Research and Industry, with per-kind Industry decay as for `machineSpeed`.
  - The yield is `floor(baseYield × composed ppm / 10^6)`, at least 1.
- **Rules**:
  - `manual.yield.bound`: `yield × cap ≤ maxShareOfMinerPpm × level-1 Miner output per second`, at every reachable yield level.
  - `start.reachable`: `startingCash + SalePricing.Value(baseYield × cap × startWindowMs / 1000, orePrice, 10^6) ≥ BuildPricing.Price(Miner, owned 0, index)` with no discounts, for every index ≤ `maxIndex`.

  Both are gated on revision ≥ 3 and replace `build.start.affordable`.
- **Offline**: no manual mining offline. The simulator is unchanged, because mining is a command.
- **Session log**: counted, ignored and mined per minute, aggregated (no per-click events).
- **Strategies**: every strategy may mine by hand in its first seconds of a run up to the first Miner. Casual mines at a pinned fraction of the cap. A probe "mining-only" is reported as orientation, and `manual.yield.bound` must keep it far below the chain.
- **UI**: `factory-ore-vein` in the Factory view.
  - Activation: pointer click, touch tap, keyboard Space/Enter, and holding any of them.
  - Size: at least 44 pt.
  - Accessible name: "Mine Iron Ore, +y per click", plus "store full" when blocked.
  - Feedback: a "+y" text pulse throttled to the cap.
  - Blocked: the full-store reason appears next to the vein.

  F004 owns the art.
- **Performance**: at most `cap / fps` commands per frame (≤ 1 at 10/s and 60 fps). The command path allocates nothing on the hot path, and the settle cost is that of any command.

## F5-R20: Mining review amendments (MIN, VEIN; owner decision D-P)

These amendments come from the Economy (MIN) and UI/UX (VEIN) reviews of commit `94ba414`. Where F5-R19 disagrees, this section wins.

- **Milli-yield (D-P, MIN-1, MIN-2)**:
  - `manualMining.baseYieldMilli` (initially 100, i.e. 0.1 Ore). A transient `MiningRemainderMilli` sits next to the rate counter.
  - Each counted activation adds the effective milli-yield, and every full 1,000 moves one Ore.
  - The effective milli-yield is `floor(baseYieldMilli × composed ppm / 10^6)`, at least 1. The validator requires that every purchasable `manualYield` level changes it (no dead level).
  - Orientation numbers (the T345 tuning decides): a cap of 10/s and 100 milli give 1 Ore/s, `startWindowMs` 60,000, a Miner prestige growth of at most 1.05 with `maxIndex` ≤ 20.
- **Bound reference (MIN-3)**:
  - `manual.yield.bound` compares the maximum mining Ore/s (yield with the product of the maximum Research and Industry `manualYield` tables, × cap) with `maxShareOfMinerPpm` × the output of one Miner at `referenceMinerLevel` (content, initially 10).
  - The Miner side is computed with the maximum Industry speed table.
  - `maxShareOfMinerPpm` ≤ 3,000,000 is structural.
- **Burden (MIN-4)**:
  - `maxIndex` and the Miner prestige growth are tuned so that no hand mining is needed for the first Miner below about index 5. Above that, at most `startWindowMs` of mining is needed.
  - There is no hold latch, because a latch would turn mining into an idle income.
- **Production objectives (MIN-5)**: mined Ore never advances a production objective, with a test.
- **Acceleration (MIN-8)**: the claim in F5-R19 is corrected. Rapid clicks hit the timeline-second cap, so under development acceleration clicks gain up to N×. This is development-only and flagged in the log, and there is no fix.
- **Trace (MIN-6, MIN-9)**:
  - New strategies: chain+mining (continuous mining in active windows at a pinned fraction of the cap, runs 1–5) and mining-only (orientation, expected "not reached" or far below the chain).
  - Gate: chain+mining beats the chain by at most `miningAdvantageMaxPpm` in time to each threshold.
  - The mining income share per run is reported.
  - A unit probe checks first-Miner reachability at `maxIndex`.
- **Partial counting (MIN-10)**: `counted = min(n, capRemaining, activations whose whole Ore fit)`. Rejected activations do not use the cap. The result reports counted, ignored (cap) and blocked (full or limit).
- **Rate window (MIN-7)**: the reset after load (at most one extra second) and the bursts at second boundaries are accepted and documented.
- **Vein placement (VEIN-1)**: the vein is a screen-space overlay control anchored to a safe-area corner of the Factory view, outside the slot rectangle and not moving with pan or zoom. When a sheet covers it, it is hidden and leaves the focus order, and Space/Enter never activate a hidden vein.
- **Gestures (VEIN-2)**:
  - Pointer capture on press, so pan and slot taps ignore that pointer.
  - A second touch pointer, such as a pinch, cancels the hold.
  - Leaving the vein while held ends the hold.
  - A press that starts elsewhere never mines.
- **Exact-value gestures (VEIN-3)**: the vein is excluded from `AttachExactValue`. A 600 ms touch hold mines and opens no peek. The stock stays inspectable where it is shown.
- **Keyboard (VEIN-4)**: KeyDown starts and KeyUp ends the hold. Accrual uses the timeline at the cap, not OS key repeat. KeyDown and NavigationSubmit are de-duplicated per frame.
- **Hold accrual (VEIN-5)**: the first press counts immediately. Then one activation counts per 1/cap s of run timeline while held, with at most one catch-up per frame. A deterministic test uses the injected timeline.
- **States (VEIN-6)**:
  - A subtle "max rate" cue in the pulse when activations are ignored.
  - The `value.limit` and lifecycle copy appears next to the vein.
  - "Store full" clears as soon as there is room, with no layout jump.
  - Disabled during a dialog, with a reason when focused.
  - A soft-disabled vein stays focusable, and a press shows the reason.
- **Feedback (VEIN-7)**:
  - One pooled label updates in place ("+0.1", or "+0.1 ×n" during a hold) with a short fade or no animation.
  - The platform reduced-animation preference is honoured where Unity exposes it. Otherwise the default is static text.
  - No colour-only state and no more than 3 flashes per second.
  - The progress indicator shows the remainder in tenths.
- **Screen reader (VEIN-8)**: the name uses the exact yield. Nothing is announced per click, only on state changes (blocked, unblocked, yield changed).
- **Focus (VEIN-9)**: the vein comes after the HUD strip and before the tab bar, with a non-colour focus ring. A hold never traps focus, and Tab ends a hold.
- **Text scale (VEIN-10)**: the blocked text sits inside the vein container and wraps. There is no fixed height, and the container grows at its end (T249).
- **Upgrades (VEIN-11)**: the label shows the current yield after an upgrade.
- **F004 hand-off (VEIN-12)**: the stable name `factory-ore-vein`, the `Mine` command, the three reasons, and the pulse, progress and blocked elements. Invariants: a screen-space control, pointer capture, the hold-end conditions, the cap in the command (never in the view), the exact name, no per-click announcement, a coalesced pulse with a static fallback, at least 44 pt.
