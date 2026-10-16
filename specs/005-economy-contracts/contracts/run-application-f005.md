# Run application contract: Feature 005 delta

This contract extends `specs/003-first-factory-prestige/contracts/run-application.md`. That contract stays authoritative for everything not changed here. Every command keeps the F003 order:

1. `lifecycle.blocked`;
2. validation;
3. settle pre-check;
4. one checked transaction;
5. settle.

A rejection changes nothing.

## New and changed commands

| Command | Preconditions (rejection) | Effect |
|---|---|---|
| `Sell(resourceId, quantity)` | resource unlocked (`resource.locked`); `quantity ≥ 1` (`sell.quantity-invalid`); `quantity ≤ stock` at the command instant (`stock.insufficient`); Cash stays ≤ 10^18 (`value.limit`) | Removes `quantity` units and adds `SalePricing.Value(quantity, basePrice, salePricePpm)` to Cash. The result reports units and Cash. Settles. |
| `Mine(activations)` | check order as implemented (#385, 2026-10-14): lifecycle (`lifecycle.blocked`); `command.invalid` (no `manualMining` in the content, or `activations` below 1); `resource.locked` (the mined resource); then the rate cap; the yield fits the store (`storage.full`); Ore stays ≤ 10^18 (`value.limit`) | `counted = min(n, cap remaining in the current timeline second, activations whose whole Ore fit the store)`. Activations rejected for a full store or the numeric bound do not use the cap. Adds `counted × effectiveYieldMilli` to the transient remainder, moves `floor(remainder / 1000)` Ore to stock in one checked transaction, and settles. The result reports counted, ignored (cap), blocked and Ore added. Mined Ore never advances a production objective. |
| `SellAll(resourceId)` | resource unlocked; stock ≥ 1 (`stock.insufficient`); Cash bound | Same as `Sell` with `quantity = stock` at the command instant. |
| `Build(slot, machineId, expectedPrice = null)` (changed) | F003 checks; when `expectedPrice` is given, it must equal `BuildPricing.Price(...)` at the command instant (`build.price.changed`). The UI always passes it, and `null` means the price at the call instant. | Charges the price once. `investedCash` is the price paid. |
| `AcceptContract(offerId)` (changed) | F003 checks; not in cooldown (`contract.cooldown`); `required ≤ current storage capacity` (`contract.capacity`); a bonus offer must be inside its window and not consumed (`contract.bonus.unavailable`). Normative check order (#388, 2026-10-14): `command.invalid` (unknown, locked or exhausted) → `contract.active-exists` → `contract.cooldown` → `contract.bonus.unavailable` → `contract.capacity` → `value.limit`; the snapshot `AcceptReason` uses the same order | Fixes `requiredUnits`. For a production objective it sets `Produced = 0`. `AutoDeliver = false`. |
| `DeliverContract()` (changed) | delivery objective only (`command.invalid` for a production objective, which the snapshot never offers); F003 checks | Shared `ContractRewards.Grant`. Starts the offer cooldown. |
| `AbandonContract()` (changed) | F003 checks | Clears the active contract and starts the offer cooldown (FR-005). |
| `SetAutoDeliver(bool)` | an active delivery-objective contract (`contract.none-active`; `command.invalid` for a production objective); for `true`, `contract.expired` when the timeline is already past the deadline, then the `DeliverContract` worst-case pre-check (`ContractRewards.CheckGrant` plus `SettleFits` with the reward Cash and the consumed stock), else `value.limit` | Sets the flag, then settles. If the stock is present, the settle delivers at t = 0. |
| `ConfirmPrestige()` (changed) | `FV ≥ PrestigeThreshold.For(min(PrestigeCount, MaxIndex))` (`prestige.below-threshold`) | F003 transition. The candidate has `PrestigeCount + 1`. |

Simulator-driven outcomes (no command):

- **Auto-delivery**: at the first tick before the deadline boundary event with `stock ≥ requiredUnits`. Simulation order decides; there is no real-time compare. Within the settle loop the order is completions → auto-delivery → auto-sell with a reserve (research F5-R05). An overflow records `AutoDeliverBlocked(value.limit)` and retries at the next instant.
- **Production completion** (T327): every machine completion that outputs the objective's resource adds its units to the scratch count in O(1) (positional, no string lookups). Mined, bought or sold units never count, and the units stay in stock. The settle loop checks the count once per pass, right after the completions, and completes the objective at the instant `Produced ≥ requiredUnits` with the same reward plan as a delivery (`ContractRewards`, FR-009), no stock consumed, the cooldown stamped with `RealUtc(tick)`. `ContractBoard.TryCountProduced` is the only writer of `Produced`: it caps at the required quantity with checked math, bumps `Version`, and when the simulator decided the completion it runs `CheckComplete` first and then ends the contract through the shared path, recording the outcome. The deadline race is the simulation order: the expiry boundary at `deadline + 1 ms` is processed before the settle of its tick, so a unit completed at the tick of the expiry is too late and one completed at the deadline counts. A reward that cannot be granted (`value.limit`) blocks the completion like a blocked auto-delivery: the count stays full, the contract stays active, `ActiveContractSnapshot.CompletionBlocked` is true and every later instant retries.
- **Expiry**: grants nothing and starts the cooldown.
- **Bonus** (T327): the unlock pass evaluates the trigger of every bonus offer that has not fired in the run (any `UnlockCondition` kind, same semantics as the unlock candidates), fires it once, and stores the window end `RealUtc(tick) + windowMs` as a UTC stamp, committed through `ContractBoard.TryTriggerBonus`. The mapping is evaluated once per firing unlock pass, never per completion. A lapsed trigger never fires again in the run. Cooldown ends and bonus window ends are **not** boundary events, and snapshots and `AcceptContract` evaluate them lazily against the timeline.
- **Reward Cash** (all paths): `MulDivFloor(SalePricing.Value(required, basePrice, salePricePpm), rewardRatioPpm, 10^6)`.
- **Offline summary** (T327): `RunOfflineSummary` gains `AutoDelivered`, `ProduceCompleted`, `Expired`, `ContractRewardCash`, `ContractRewardRp` and `ContractRewardItems`; `RunOfflineContractKind` gains `ProduceCompleted`, and `RunOfflineContractOutcome` carries `EndedUtcMs`, `CashReward`, `RpReward` and `ItemsGranted`. The reward Cash is part of `CashDelta` and not of `SalesIncome`.

All of these appear in `ContractsSnapshot.LastOutcome` (`ContractOutcome`, a value type: kind, offer, required, completion index, end instant, Cash, RP and Item count; kept until the next acceptance, never persisted) and in the session-log events. The session raises them from a before/after comparison of `ContractBoard.OutcomeSerial` and the blocked state of the active contract after each successful advance (`AutoDeliver`, `ProduceComplete`, `Expire`; `Blocked` on the change into the blocked state only, AR2-4); the simulator never calls the sink. A manual `DeliverContract` records no outcome and raises no outcome event.

`SettleFits` counts the reward Cash of an active production objective that the settle after a command can complete (the waiting machines can output at least the units still missing), the same way it counts an auto-delivery, so a command is never followed by a settle overflow through a production reward. `ActiveContractSnapshot.StockUnits` and `MissingUnits` are meaningful for delivery objectives only.

Persistence additions:

- `RunPersistence.RecordDepartureAt(utcMs)` records a departure at `clamp(utcMs, currentDeparture, timelineEnd)`. The host follows it with `ResumeAndReconcile`.
- `Autosave` keeps a minimum interval of `liveGapThresholdMs` (#354).

## Read models (new or changed)

- **`EconomySnapshot`**: adds `SellSteps` and, per resource, `SellPreview(step)` with units, Cash and an availability reason.
- **`RunFactorySnapshot`**: the build preview carries `Price` (from `BuildPricing`), `OwnedOfType` and `PriceCapped`. It is cached per structure revision.
- **`ContractsSnapshot`**: per offer it carries `Objective`, `Class`, `CooldownRemainingMs`, `BonusWindowRemainingMs`, `Deliverable` (stock ≥ required for delivery objectives), `CanAutoDeliver`, `AutoDeliver` and `Produced / Required`.
- **`GoalsSnapshot`** (new): `Next` (milestone index or threshold), `Target`, `Reached`, `PercentFloored`, `FactorySaleAvailable`, `AllReached`.
- **`PrestigePreview`**: adds `Threshold`, `NextThreshold` and `PrestigeIndex`.

## Stable reason codes

The F003 codes, and every code that is already a constant in code, are listed in `docs/contracts/run-reason-codes.md`. That file is authoritative and pinned by `ReasonCodeContractTests` (#355): set equality with the code constants, and every rejection code has player copy. The rows below are the F005 codes. Each task that introduces a constant appends its row to that file in the same commit.

| Code | Source | Since |
|---|---|---|
| `sell.quantity-invalid` | RunReasons | F005 |
| `build.price.changed` | RunReasons | F005 |
| `contract.cooldown` | RunReasons | F005 |
| `contract.bonus.unavailable` | RunReasons | F005 |
| `contract.capacity` | RunReasons | F005 |
| `storage.full` | RunReasons | F005 |
| `contract.dropped` | RunPersistence load summary (informational) | F005 |

The `import.*` family stays in `save-v2-and-v1-import.md` and is pinned by the same test against that file.

## Determinism obligations (tests)

- **Partition invariance**: one advance equals any partition, live or offline, for each of these:
  - auto-delivery;
  - production objectives;
  - the auto-sell reserve;
  - cooldown and bonus-window boundaries;
  - a large live gap reclassified as an offline return.
- **Shared arithmetic**: manual and automatic delivery produce identical reward deltas (FR-009).
- **Sale neutrality**: `Sell` of the full stock changes Factory Value by 0. No sale increases it.
- **Remove and rebuild**: `Build`, `Remove`, `Build` never increases Cash + FV (ECO-7) under any scaling.
