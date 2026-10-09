# Research: Economy and Contracts (Feature 005)

Phase 0 decisions for [plan.md](plan.md). Each entry gives the decision, the rationale and the rejected alternatives. Inputs:

- the spec,
- [reviews/discovery.md](reviews/discovery.md), which covers both review waves,
- the F003 artifacts,
- the code at `a41c53f`.

Numbering continues the F003 convention with the prefix `F5-R`.

## F5-R01: #356 refactor shape and gate

**Decision**: The refactor is behaviour-preserving and lands before any F005 behaviour change. It touches three areas.

- **`RunPersistence`** (1,919 lines) becomes a `partial class` spread over these files:
  - `RunPersistence.cs`: lifecycle core, the admission table and the state machine, which is not split further;
  - `RunPersistence.ColdStart.cs`;
  - `RunPersistence.Transition.cs`.

  The types `RunPersistenceOutcome`, `Status` and `Reasons` move to `RunPersistenceTypes.cs`. `OfflineRunReturnReconciler` and `NoCredit` move to `OfflineReturn.cs`. A new internal `SafeRunStore` wraps the `Safe*` store calls that turn exceptions into `CommandResult`.
- **`RunSnapshots`** splits into `.Factory`, `.Economy` (rates and market), `.Contracts`, `.Items` and `RunDerivation.cs`.
- **`RunState`**: the command-path mutators become `internal`. These include `TryAddCash`, `TrySpendCash`, `TryAddRp`, unlock, auto-sell, sell, build, upgrade, remove, deliver, the expansions and research purchase. The Save restore paths stay `public`: `TryRestoreInvestment`, `TrySetResearchLevel`, `TryAddEffect`, `TrySetIndustryLevel`, `ContractBoard.TryRestoreActive` and `TrySetCompletionCount`. `IdleFactory.Core` gains `InternalsVisibleTo` for the Edit Mode and Play Mode test assemblies, because five Play Mode files seed state through the mutators. Unity code calls none of them.

The gate has three parts:

- an Edit Mode `PublicApiSnapshotTests`, whose reflection dump of the public surface of `IdleFactory.Core` must equal the committed baseline except for the listed visibility changes;
- the unchanged Edit Mode, Play Mode and StandalonePlayer suites;
- an architect review of the diff.

**Rationale**: F005 adds sell, auto-deliver, threshold, cooldown and log hooks to exactly these files. If the files are split first, every later work unit edits a small, owned file, and the new mutators are internal from the start (F-A2, F-A7).

**Alternatives**:
- Refactor while adding features: rejected, because it mixes behaviour and structure in the same diff and QA cannot prove equivalence.
- A separate feature: rejected by the owner (D-F).

## F5-R02: #353 measurement first, then the hot-path decision

**Decision**: Work unit 0, Option 0, runs before #356.

- **Base**: `a41c53f`.
- **Host**: quiet, with the editor and the emulator closed, and the state of both recorded.
- **Workload**: W003-B on the Windows Mono player, median of 5 over at least 3 runs, with a phase attribution (step scan, `CompletionPass`, run predicate, `AutoSell`, lookups) through Stopwatch accumulators in a development build.
- **Emulator**: W003-B is also measured there, from a development player with the test catalog.
- **Record**: `reviews/w003b-measurement.md`.

Option 1 is an index-based hot loop as an additional API. If the measurement chooses it, the decision is recorded before the work unit that changes selling and auto-sell (F5-R04, F5-R05). It is implemented as an Architect-approved work unit that preserves bit-identical results against the committed W003 fixtures and hashes. Option 3, the steady-state jump, stays out of F005.

**Rationale**: D-H and the Performance condition C2. The settle path changes in F005, so a later measurement would mix causes.

## F5-R03: Content schema version 3

**Decision**:
- The packaged content moves to `contentSchemaVersion 3`, still strict: unknown or missing members are diagnostics.
- The v2 reader is retired for gameplay. Saves reference IDs only, so no save path needs content v2.
- The version dispatcher accepts 3 and rejects 2 with `content.schema.unsupported`.
- The legacy v1 catalog stays for V1 import.

Additions, detailed in [contracts/gameplay-content-v3.md](contracts/gameplay-content-v3.md):

- `runStart.sellSteps` (ordered positive integers plus the `all` token) and `runStart.liveGapThresholdMs`.
- `contracts[]`:
  - the closed `kind` vocabulary (`plate`, `gear`, `timedBar`) is replaced by `objective` (`deliver` or `produce`), optional `durationMs` (timed), `class` (`standing` or `bonus`), `rewardRatioPpm`, `cooldownMs`, and `bonus { trigger, windowMs }` for bonus offers;
  - the RP rewards stay as a curve, and the Item rewards move to a catalog-level `contractItemLadder { indices[], perRunCap }`.
- `machines[].buildPrice { base, ownedGrowthPpm, prestigeGrowthPpm, cap }`. Upgrade cost (`cost`) is unchanged.
- `prestige.threshold { base, growthPpm, maxIndex }`. `ipCurve` is unchanged (FR-020), with the fixed divisor `10,000,000`.
- `milestones[]`: ppm fractions of the run's threshold, strictly ascending, each in `(0, 1,000,000)`.
- `industryUpgrades[]`:
  - `valuePerLevel` decays by `decayPpm` per level;
  - each upgrade gains `practicalCeilingLevel` and `minGainPpm`;
  - `maxLevel` stays as the technical bound.
- `chainDominanceMarginPpm` (> 0) for the static check in FR-017.

New validator rules:
- `contract.ratio.standing` (R in [1.0, 1.5]), `contract.ratio.bonus` (R ≤ 2.0) and `contract.ratio.produce` (0 < R ≤ 1.0);
- `contract.quantity.capacity` (required ≤ the highest reachable storage capacity at every reachable index);
- `ladder.uptime` (ECO-6);
- `milestone.order`;
- `prestige.threshold.bound` (T_maxIndex ≤ 10^18);
- `build.start.affordable`;
- `chain.dominance`;
- `industry.ceiling.level`.

The Industry rule "practical ceiling ≥ 4.0× the IP of runs 1–4" needs trace data, so it is a balance-trace gate (F5-R13), not a static rule.

**Rationale**: The strict v2 reader cannot carry optional members (the same reason as Save V3), and the contract vocabulary must change shape. A schema version keeps the diagnostics exact.

**Alternatives**:
- Optional members in v2: rejected, because it weakens strictness.
- Keeping the `kind` labels and deriving the objective from them: rejected, because the labels hard-code three offers.

## F5-R04: One sale price

**Decision**: A pure `SalePricing.Value(units, basePrice, salePricePpm)` returns `floor(units × base × ppm / 10^6)` through `MulDivFloor`. It is the only sale arithmetic, and three callers share it:

- `RunSimulator.AutoSell`: one sale per resource per instant, after the instant's expiries;
- the new `RunCommands.Sell`;
- `FactoryValuation`: stock at the effective price.

The `Sell(resourceId, quantity | All)` checks run in this order:
1. lifecycle;
2. resource unlocked, else `resource.locked`;
3. quantity ≥ 1, else `sell.quantity-invalid`;
4. quantity ≤ stock, else `stock.insufficient`;
5. the cash bound, else `value.limit`.

It then runs the settle pre-check (`SettleFits`) with the sold stock subtracted, commits, and settles. `All` takes the stock at the command instant. The UI confirmation for an active delivery-contract shortfall (FR-003) is a presenter step before the command. The command itself takes no confirmation flag.

**Rationale**: Full-stock neutrality and "no sale raises FV" follow directly when valuation and sale share one function and one floor.

## F5-R05: Auto-deliver and production objectives inside the simulator

**Decision**: `RunSimulator.ProcessTimestamp` order per instant:

1. expiries (deadline, Item and Overdrive end, bonus-window end);
2. priority completions, which also count the production objective;
3. auto-delivery;
4. auto-sell, with a reserve;
5. the retry pass (OD-2);
6. the run predicate.

- **Reserve**: while the active contract is a delivery objective with auto-deliver on, auto-sell of its resource keeps `min(stock, requiredUnits)`.
- **Production objective**: one `long` counter on the active contract. It increments by the units output of the target resource in the completion pass, which is an O(1) compare per completion, and it is satisfied when it reaches `requiredUnits`.
- **Delivery and reward arithmetic**: both paths use one pure `ContractRewards.Grant(...)`, extracted from `TryDeliverContract` (FR-009). It runs on the simulator's scratch `_stock`, `_cash` and a new `_rp`. Board and Item changes commit at the end of the advance, as `TryExpire` does today.
- **Instants**:
  - Delivery at most once per advance, because the flag and the contract clear.
  - Cooldown and bonus windows are real-UTC timestamps, like deadlines (F003 R05). An auto-delivery during offline reconcile happens at simulated time `t` of the segment.
  - Its real instant is `d + ceil(5t/4)`, the inverse of the 4/5 mapping, capped at the return instant `r`. That is a pure function of `(d, t)`, so the result does not depend on the partition.
- **Boundary events**: deadline, cooldown end and bonus-window end are boundary events, ordered `(kind, id)` as in GD-3.

**Rationale**: Segment invariance (FR-012, SC-003) needs evaluation at the exact completion instant. A command- or frame-level check depends on the frame rate. This was the Architect finding, and it settles the Performance concern; condition C3 is met by measuring with auto-deliver active (F5-R14).

**Alternatives**: Boundary-only evaluation: rejected, because it is not partition-invariant when stock crosses the requirement mid-segment.

## F5-R06: Build pricing

**Decision**: A pure `BuildPricing.Price(def, ownedOfType, prestigeCount)` in Application computes `ceil(base × ownedGrowthPpm^owned × prestigeGrowthPpm^prestigeCount / 10^6^(owned+prestigeCount))`.

- It uses one ppm accumulator (`MulDivFloor` per factor), then one final ceiling, then `min(cap, ...)`, and checked math throughout. An intermediate above 10^18 rejects with `value.limit`.
- The existing discounts (Toolmaker, Research `upgradeCost`) then apply with the F003 round-up rule (a positive price never becomes 0).
- `RunFactorySnapshot` and `Build` both call it, so the displayed price is the charged price.
- `Build` gains an `expectedPrice` argument. A mismatch rejects with `build.price.changed` and changes nothing (FR-014).
- Owned count and prestige count are derived state, so nothing new is persisted.
- Snapshots cache the price per structure revision (Performance C6).

## F5-R07: Rising threshold and milestones

**Decision**:
- **Threshold**: `PrestigeThreshold.For(n) = ceil(base × growthPpm^n / 10^6^n)` with checked math, where `n = PermanentState.PrestigeCount`, which is already persisted.
- **Preview and confirm** read `For(PrestigeCount)`. If `PrestigeCount` reaches `maxIndex`, prestige rejects with `prestige.maxed`.
- **IP** keeps the F003 formula with the fixed divisor. The candidate carries the incremented permanent state, so the existing durable protocol (`EveryProtocolStepFailingOnce`) already makes index and IP exactly-once. A new test proves that a retry does not double the index.
- **Milestones**: values are `floor(threshold × fractionPpm / 10^6)`, derived per run. A new `GoalsSnapshot` holds the next goal, the amount, the percentage (floored) and the reached flag.
- **Notices**: the one-time notice is presentation state keyed by `(runId, milestoneIndex)`. It is not persisted: after a load, already-passed milestones do not re-notify, because the presenter seeds them from the current Factory Value.

## F5-R08: Industry with diminishing returns

**Decision**:
- **Value**: an Industry upgrade's per-level value is `valuePerLevel × decayPpm^(level−1)` (floored), summed. `EffectiveIndustryValue` is a precomputed table per upgrade, built at catalog construction up to `maxLevel`.
- **Practical ceiling**: the cumulative IP to `practicalCeilingLevel` for each upgrade. The validator checks that `practicalCeilingLevel` is the first level whose marginal value is below `minGainPpm` (`industry.ceiling.level`).
- **The 4.0× rule** is asserted in `BalanceTraceTests` against policy r2.

**Rationale**: D-C. A closed-form table keeps the hot path free of exponentiation.

## F5-R09: Save V3

**Decision**:
- The writer always writes `schemaVersion 3`. The reader accepts 2 and 3 and stays strict per version.
- **V3 adds**:
  - `activeContract.autoDeliver` (bool), `activeContract.produced` (long, production objective);
  - `contractCooldowns[] { offerId, readyAtUtcMs }`;
  - `bonusOffers[] { offerId, state: triggered|consumed, windowEndUtcMs }`.
- **V2 load**: auto-deliver false, no cooldowns, no bonus state, produced 0.
- **Out-of-range and malformed values**:
  - Booleans are strict, and numbers must lie in [0, 10^18].
  - A timestamp that is not later than the save's timeline start is clamped to "ready", not rejected, because clocks move.
  - Every other value fails closed with the existing codes.
- **Offer removed by content**: an active contract, cooldown or bonus entry whose `offerId` is unknown in the loaded content is dropped. The load reports this in the existing return/load summary and does not fail (FR-034).
- **Durability**: generation, promotion and the durability protocol are unchanged. A failed V3 write keeps the previous generation.
- **Fixtures**: V2 fixtures (all existing, including W003 and `prestige-ready`) must load, and new V3 fixtures cover every member and each fail-closed case.

**Rationale**: D-G and the Architect finding: the strict reader turns any additive field into a version change.

## F5-R10: Large live gap

**Decision**:
- **Detection**: `FactoryRuntimeHost` measures the UTC wall delta between frames through the existing clock port, not `realtimeSinceStartup`, which may stall during sleep.
- **Threshold**: a delta strictly greater than `runStart.liveGapThresholdMs`. The threshold is packaged content, so the player cannot edit it.
- **Conversion**: the host calls a new Core operation, `RunPersistence.RecordDepartureAt(previousFrameUtcMs)`, followed by the existing `ResumeAndReconcile`. The departure is never later than the timeline end and never earlier than the current departure.
- **Negative deltas** credit nothing.
- **Time scale**: the dev time scale multiplies only the simulated delta after this check, so the scale never counts toward the threshold.
- **#354**: `Autosave` gains a minimum interval guard that reads the same content value. #354 also adds the failing-write adapter test and the quit-then-focus-lost order test.

## F5-R11: Development time acceleration

**Decision**:
- **Assembly**: a new assembly `IdleFactory.DevTools` with `defineConstraints: ["IDLEFACTORY_DEV"]`, referencing `IdleFactory.Unity` and never referenced back.
- **Seam**: `IdleFactory.Unity` exposes a null-default `ITimeScaleSource` seam. DevTools registers it in `RuntimeInitializeOnLoadMethod`. Its content:
  - an overlay control (1, 10, 60, 600, Max);
  - the permanent "ACCELERATED ×N" marker;
  - a log header and event flag.
- **Clamp**: the host clamps the scaled delta to S_max = 10 simulated seconds per frame.
- **Builds**: `PlatformBuildCommands` gains development variants that add `IDLEFACTORY_DEV` for that build only. The editor sets it through a menu toggle that writes the scripting define symbols.
- **Release proof**: a new `scripts/check-release-build.ps1` scans a non-development build. It fails if `IdleFactory.DevTools.dll` exists in `Managed/`, or `libil2cpp`/metadata contains the DevTools type names. In-player tests cannot prove absence, because the test player is a development build.

**Alternatives**: Plain `DEVELOPMENT_BUILD` gating: rejected, because test players and profiling builds are development builds that must stay unaccelerated by default.

## F5-R12: Session log

**Decision**:
- **Core**: a port `IRunEventSink` with a no-op default on `RunSession`, plus readonly event structs whose fields are allowlisted and typed (the FR-029 groups).
  - Events are raised by commands, persistence outcomes, the offline summary and the prestige transition.
  - Samples come from the host at timeline minutes 10, 15, 30, 60, 90 and 120, plus milestones.
  - Bottleneck seconds come from per-machine wait counters that the simulator already keeps, exposed as derived per-resource totals read at sample time.
  - The simulator never calls the sink, so there is zero work in `Advance`.
- **Unity adapter `SessionLogWriter`**:
  - **Buffering**: an in-memory ring buffer of structs, serialized to JSON lines with a CRC32 per line. A flush happens at lifecycle points and at most every 30 s of wall time, off the frame path, as a coalesced write after the frame.
  - **Storage**: two rotating ~128 KB segment files under `persistentDataPath/IdleFactory/SessionLog/`, outside `Checkpoints/`. A torn last line is skipped on export.
  - **Failure handling**: every call is wrapped in try/catch, there is a drop counter, and the writer never touches the save path.
- **Setting**: on/off is stored in `PlayerPrefs` (per device, not in the save) and defaults to on.
- **Export**:
  - It writes `Exports/session-log-<utc>.jsonl` under the same root and shows the path.
  - On Android it also offers a share intent through an `androidx.core.content.FileProvider` entry in a custom `AndroidManifest.xml` with `res/xml/file_paths.xml`.
  - The backup rules (`fullBackupContent` and `dataExtractionRules`) exclude `SessionLog/` and `Exports/`.
  - `scripts/check-release-build.ps1` checks the merged manifest of an Android build for the exclusion. This is the release gate from FR-031.
  - The share intent is planned as a separate work unit. If the plan review rejects it as too costly, the path-only export stays, and the Android export evidence uses `adb pull`.
- **Session id**: a random 128-bit value per app start (`System.Random` seeded from a time-based GUID). It is never persisted beyond the log.

**Rationale**: Security conditions 1–5 and Performance C5.

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
- The full matrix is `[Category("BalanceFull")]` and may be excluded from the per-Issue gate if it exceeds 5 minutes of Edit Mode time. The parent then runs it once per batch and before content approval. The fast gate keeps the chain strategy at one offset plus the static content rules.
- The non-rounding price variant replaces `var.price-80`.

## F5-R14: Performance workloads and budgets

| Item | Definition |
|---|---|
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
- **Contract**: [contracts/run-application-f005.md](contracts/run-application-f005.md) adds the F003 omissions `command.invalid`, `checkpoint.content` and `discard.unavailable`, and the F005 codes:
  - `sell.quantity-invalid`;
  - `build.price.changed`;
  - `contract.cooldown`;
  - `contract.bonus.unavailable`;
  - `prestige.maxed`.
- **Pin test**: `ReasonCodeContractTests` collects every `const string` in `RunReasons` and the persistence reason classes by reflection. It parses the stable-code tables of both contract files and asserts set equality. It also asserts that `RunRejectionCopy` has copy for every code.
