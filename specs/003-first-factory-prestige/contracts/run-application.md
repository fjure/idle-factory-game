# Contract: Run Application Commands and Read Models

**Consumers**: Unity presenters, input adapters, lifecycle adapter, balance trace, and Play Mode and player tests.
**Normative for**: FR-003–FR-026 behaviour at the application boundary, SC-001 and SC-002.

The contract has two parts. `RunSession` in `IdleFactory.Run.Application` owns the live state, the simulator, the timeline and revisions. `RunCommands` is the only mutation API. Every command follows the same four rules:

1. It applies at the current timeline instant, after that instant's simulation interval has been processed.
2. It validates every precondition before it mutates anything.
3. It applies all its changes in one checked transaction, then *settles* the session (auto-sales and the run predicate, R01).
4. It returns `RunCommandResult<T>` with an outcome and a player-readable reason code from the list below.

A rejection never changes state. This follows the Feature 001 `CommandResult` pattern, but `RunCommandResult` is a new type with its own reason codes (including `value.limit` and `lifecycle.blocked`); the legacy `CommandResult` and `CommandOutcome` stay unchanged until R16 retires their gameplay use (GD-2).

## Commands

| Command | Preconditions (each failure is a named rejection) | Effect |
|---|---|---|
| `Advance(activeTicks)` | `≥ 0` | Runs the event loop (R01), expires timers against the timeline and increments revisions. Timeline boundaries reach the simulator as simulation-time events through the same `RunSimulator.Advance` entry point that offline return uses (R05). Cycles beyond the event bound complete as batches, partially when inputs or capacity limit them (R03, OD-6, OD-9). Overflow (the simulator's `ArithmeticOverflow`) returns `value.limit`, and the state stays unchanged. |
| `Build(slot, machineId)` | slot exists, is free and is within `SlotCount`; type unlocked; `cash ≥ cost`, where the cost is the level-1 cost after `upgradeCost` (owner decision 2026-09-30) | Charges the cost once, creates the machine at level 1 with progress 0, appends it to the priority order and sets `investedCash = cost`. |
| `Upgrade(instanceId)` | owned; `level < maxLevel`; `cash ≥ cost` | Charges the cost, increments the level, adds the cost to `investedCash` and rescales progress. The result reports the new rate, whether a milestone was reached, and the next cost. |
| `Remove(instanceId, confirmed: true)` | owned; confirmation flag | Refunds `floor(investedCash × refundPpm / 10^6)`, drops progress, frees the slot and closes its priority position. |
| `SetActive(instanceId, bool)` | owned | Toggles the machine; progress is kept. |
| `ReorderPriority(permutation)` | exact permutation of the current machines | Replaces the order. |
| `SetAutoSell(resourceId, bool)` | resource unlocked | Changes only that setting, then settles, which may sell immediately. |
| `UpgradeStorage()` / `UpgradeFactory()` | next level exists; `cash ≥ cost` (after `upgradeCost`) | Charges the cost, adds it to the invested amount and raises capacity or slots. |
| `AcceptContract(offerId)` | offer unlocked; no active contract | Fixes `requiredUnits` and the deadline (`timeline + durationMs` for `timedBar`). |
| `DeliverContract()` | a contract is active; `timeline ≤ deadline`; `stock ≥ requiredUnits` | Consumes the units and grants every reward once, in one checked transaction, then increments the completion count. |
| `AbandonContract()` | an untimed contract is active; the UI confirms first | Clears the active contract with no reward and no penalty; the completion count is unchanged (OD-4). |
| `ActivateItem(itemId, target)` | count ≥ 1; target valid for the definition (for a `machineType` Item, `target` is a machine ID contained in the Item's `allowedMachineIds`, otherwise `item.target.invalid`); no active effect with the same conflict key | Consumes one Item, adds the effect `[t, t + duration)` with its target key set to that machine ID (`factory` Items have no target), and rescales progress. |
| `PurchaseResearch(researchId)` | prerequisites met; `level < max`; `rp ≥ cost` | Spends RP and applies the effect immediately, with a rescale. |
| `ActivateOverdrive()` | unlocked; `timeline ≥ cooldownUntil` | Sets `activeUntil` and `cooldownUntil`, and rescales progress. |
| `PurchaseIndustry(upgradeId)` | `level < max`; `ip ≥ cost` | Spends IP permanently. Run-scoped effects apply now; starting effects apply at the next run (R12). |
| `GetPrestigePreview()` | — | Returns the FV breakdown, the IP award, eligibility with the missing value, and the reset and preserve lists (FR-022). |
| `ConfirmPrestige()` | `FV ≥ threshold` at the command instant; no transition pending | Durable transition (R09) in two steps. The command builds the complete, settled next-run candidate in memory and holds it as the session's pending transition. The old run and the permanent state stay unchanged, and commands and `Advance` reject with `lifecycle.blocked` while it is pending. The persistence layer (T105) writes and verifies the candidate checkpoint (`previousRunId` = confirmed run ID) and only then publishes it (`PromotePrestige`); `CancelPendingTransition` drops it and keeps the old run. The result reports the awarded values, or failure with the old run kept (clarified 2026-09-30 after the T102 review). |
| `PromotePrestige()` | a prestige is pending | The durable promotion (R09, T105). With the persistence layer attached it is `RunPersistence.CommitPendingTransition()`: re-read the durable active checkpoint; if its `previousRunId` equals the live `runId` the transition already happened and is published without a second award; otherwise write the candidate checkpoint (built once, same bytes and ID on every attempt, departure `max(ConfirmedUtcMs, active departure)`), read it back and verify it, and only then install a fresh read of it as the live run and settle once. Any failure keeps the old run with the transition pending (`persistence.failed`, `return.blocked`). The UI cannot publish a prestige without this write. |
| `CancelPendingTransition()` | a prestige is pending | Drops the candidate and keeps the old run. A promotion that is already durable cannot be cancelled (`transition.committed`: retry it). Once a promotion was issued, a cancel whose durable check cannot read the store fails closed (`persistence.failed`, the retry stays possible), because that promotion may already be durable; it also rebinds the session anchor to the clock so the old run's timers are measured against real time. A written but unpromoted candidate checkpoint is a pending file that the generation protocol discards at the next promotion. `SaveNow` and the suspension checkpoint never write over a durable successor of the live run (`transition.committed`), and `RecoverPreviousCheckpoint` is rejected with `transition.pending` while a prestige is pending. The new run's departure is `max(ConfirmedUtcMs, active departure)` even when the commit succeeds later; a crash after a late commit credits that gap to the new run, bounded by the offline cap. |
| `RetryPendingTransition()` | a prestige is pending | Idempotent re-check and re-promotion (R09): the same operation as `PromotePrestige()` over the persistence layer. A blocked return is retried with `RetryReturn`; the V1 import has its own retry (T106). |

Persistence commands (`SaveNow`, `RecordSuspension`, `ResumeAndReconcile`, `RetryReturn`, `ReloadAuthoritativeGeneration`, `RecoverPreviousCheckpoint`) keep their Feature 001 meanings over Save V2.

**While a prestige transition is pending** (QA-4): a second `ConfirmPrestige` and `SaveNow` are rejected with `transition.pending`, and autosave skips its write, so no checkpoint can race the candidate. `RecordSuspension` and `ResumeAndReconcile` first re-read the durable active checkpoint (R09): a verified promotion publishes the new run (a suspension then writes the new run's departure checkpoint; a return credits the new run from ITS departure, never the old run's), otherwise the old run stays, nothing is credited or written, and the transition remains retryable (`RunPersistenceOutcome.TransitionPending`). Neither path awards IP twice or credits offline time twice.

**Offline return** (T105, FR-025, R05, R09): `ResumeAndReconcile` and `RetryReturn` build the return candidate with `OfflineRunReturnReconciler` from the PERSISTED departure `d` and the captured return instant `r`: credit `max(0, r - d)` through `OfflineSegmenter`, `reconciledFromCheckpointId` = the source (the exactly-once key), departure moved to `r` and never earlier (a departure in the future credits nothing and is kept). The reconcile mutates its source, so every attempt runs on a private fresh read of the source checkpoint, and the same source bytes with the same `r` give the same candidate bytes. After the promotion is verified, a fresh read of it is installed and the session settles once (this settle, in `RunPersistence`, is the owner of the settle after a load, a return and a prestige promotion; the run creation settles in `PrestigeTransition.TryBuild` and the bootstrap, V1 import in T106). Every machine is rescaled to its effective cycle before it produces anything (load rule 2). A simulator rejection (`value.limit`) forfeits the credit instead of blocking forever: the state stays as before the absence, the departure advances to `r`, the session runs, and the summary carries the static code `offline.value-limit` (owner decision 2026-10-01); a retry or restart never attempts that credit again.

**Lifecycle seam and desktop focus** (T250, owner decision 2026-10-09; supersedes Feature 001 FR-037 for desktop focus only): what losing focus means depends on the runtime platform class, `LifecyclePlatform` (`Application.isMobilePlatform`, overridable by tests), never on the input-device profile.

| Event | Phone or tablet (unchanged: FR-037, R20, T246) | Desktop player and editor Play Mode |
|---|---|---|
| Focus loss, pause | Departure: the host flushes and stops, `RecordSuspension` writes the departure checkpoint. | Not a departure. Pause is ignored. The host keeps simulating live, the lifecycle stays `Running`, nothing is suspended. |
| Focus or pause return | `ResumeAndReconcile` credits `max(0, r - d)` once; an interval of at least 60 s opens the summary, a shorter one only updates `LastOfflineSummary` (T246); a rotation withholds the summary (R20). | Nothing: no reconcile, no checkpoint, no summary. |
| Focus loss, extra write | None. | One plain safety checkpoint (the periodic-`Autosave` command, after the host has brought the run up to date). It is not a suspension and gives no departure semantics. It skips silently while a prestige is pending or the lifecycle is not `Running`, and a failed write changes only the persistence status. |
| Regular shutdown (`OnApplicationQuit`) | `RecordSuspension` writes the departure checkpoint. | The same. The next start credits the interval once through the cold-start reconcile (`reconciledFromCheckpointId`). |
| Unclean exit | The last checkpoint is the source (FR-037 fallback). | The last checkpoint is the source: the latest safety checkpoint, else the last departure or start. Its state and its departure are from the same instant, so everything played after it is lost and its interval is credited as offline time (efficiency and cap), never twice. |

Decision (safety checkpoint): the run has no timer-driven autosave (Feature 001 A-005), and a focus-loss departure was the only checkpoint a desktop player wrote between start and quit. Without a replacement, a crash after a long session would roll the factory back to the session start. A plain checkpoint on focus loss keeps that protection at the moment a crash is most likely to follow (the player leaves the window) and does not change the offline accounting, because the departure stored with a checkpoint is the instant its state was captured. The Player setting Run In Background is on (`runInBackground: 1`): Unity then keeps a desktop player running when it loses focus and does not send a pause. Android and iOS are unaffected: iOS ignores the setting, and Android honours it only for an app that is visible but unfocused (multi-window), where the focus-loss departure still applies; a backgrounded Android app pauses regardless. No save format, reconciler or accounting rule changes.

## Rejection reasons (stable codes for UI copy)

`slot.occupied`, `slot.unavailable`, `machine.locked`, `cash.insufficient`, `level.max`, `confirmation.required`, `priority.not-permutation`, `resource.locked`, `contract.active-exists`, `contract.none-active`, `contract.expired`, `stock.insufficient`, `item.none`, `item.target.invalid`, `effect.conflict`, `research.prerequisite`, `rp.insufficient`, `overdrive.cooling-down`, `overdrive.active`, `ip.insufficient`, `prestige.below-threshold`, `transition.pending`, `value.limit`, `lifecycle.blocked`.

Further stable codes: `offline.value-limit` (the summary code of a forfeited offline credit, `RunOfflineSummary.Code`; not a rejection) and, of the persistence commands, `transition.committed` (a cancel of an already durable prestige), `persistence.failed`, `return.blocked`, `lifecycle.noop`, `checkpoint.none`, `checkpoint.invalid` and `checkpoint.version`.

## Read models (immutable; allocation rules from R14)

- **Allocation-free hot reads**: `StructureRevision`, `QuantityRevision`, `ReadCash()`, `ReadRp()`, `CopyInventory(Span/array)`, `CopyMachineStates(array)`. A machine state holds the slot, type index, level, active flag, running/paused/waiting, the blocker kind, the batch factor and the progress fraction in ppm.
- **Snapshots, built on demand**:
  - `RunFactorySnapshot`: slots, machines, costs, next-upgrade results and unlocks. (The `Run` prefix avoids a clash with the legacy `FactorySnapshot` until R16; GD-2.)
  - `RatesSnapshot`: per resource, production, consumption and net rate per minute (batches included); store usage; and the blockers per machine with their corrective actions (FR-011). `MissingInput` states when auto-sell of that resource is the cause (UX-10); `StorageFull` applies only when not even one recipe unit fits; a machine limited to a partial batch reports its last `k′` of `k` and the limiting input or the store (OD-9). Rates are shown to three significant digits; the exact values are available on inspection.
  - `EconomySnapshot`: prices and auto-sell settings.
  - `ContractsSnapshot`, `ItemsSnapshot`, `ResearchSnapshot`, `IndustrySnapshot`.
  - `OverdriveStatus`: ready, active with remaining time, or cooling down with remaining time.
  - `PrestigePreview`.
  - `RunOfflineSummary` (not the legacy `OfflineReturnSummary`; GD-2): measured, credited and effective durations; stock and Cash deltas; sales income; expired effects; contract outcomes; and limiting conditions.
  - `StartupStatus`: loaded, fresh, imported, import blocked with a rule code, or an error with recovery actions.

## Determinism obligations (tests)

- The same initial state, the same ordered commands and the same total ticks yield an identical `RunState` for one advance, for any split into partitions (including splits at event instants), and for any JSON member order.
- Competing completions resolve by `PriorityOrder` only. Slot index and the order in which machines were built never matter.
- The offline segmented advance equals the online advance over the same effective ticks, including the same boundary instants mapped through `E` (R05).
- Small scenarios match a tick-by-tick reference oracle; randomized cases use fixed seeds that a failure reports; every step conserves stock (completions minus sales, deliveries and imports); each timestamp takes at most N+1 passes (OD-2), counted by a test-only counter.

## Balance trace interface

`BalanceTraceRunner(catalog, ReferencePolicy, seedState)` issues the commands above against a manual clock and writes CSV rows at 600, 900, 1,800, 3,600, 5,400 and 7,200 simulated seconds. Each row holds: `minute, cash, fv, ip_preview, stock{ore,bar,plate,gear}, machines{type:count@maxLevel}, storageLevel, factoryLevel, contractsCompleted, itemsHeld{t1,t2,t3}, researchLevels, firstGearAtSec, eligibleAtSec`. A second-run block follows the first IP purchase, then a run 3–5 projection that spends all IP (ECO-3), and one block per sensitivity variant. The CSV bytes are deterministic. Pass/fail bands, sensitivity bounds and the policy's latency and idle rules live in `reviews/balance-policy.md` (ECO-2); the golden comparison proves determinism, and the band assertions prove plausibility.
