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
| `Mine(activations)` | lifecycle (`lifecycle.blocked`); the yield fits the store (`storage.full`); Ore stays ≤ 10^18 (`value.limit`) | `counted = min(n, cap remaining in the current timeline second, activations whose whole Ore fit the store)`. Activations rejected for a full store or the numeric bound do not use the cap. Adds `counted × effectiveYieldMilli` to the transient remainder, moves `floor(remainder / 1000)` Ore to stock in one checked transaction, and settles. The result reports counted, ignored (cap), blocked and Ore added. Mined Ore never advances a production objective. |
| `SellAll(resourceId)` | resource unlocked; stock ≥ 1 (`stock.insufficient`); Cash bound | Same as `Sell` with `quantity = stock` at the command instant. |
| `Build(slot, machineId, expectedPrice = null)` (changed) | F003 checks; when `expectedPrice` is given, it must equal `BuildPricing.Price(...)` at the command instant (`build.price.changed`). The UI always passes it, and `null` means the price at the call instant. | Charges the price once. `investedCash` is the price paid. |
| `AcceptContract(offerId)` (changed) | F003 checks; not in cooldown (`contract.cooldown`); `required ≤ current storage capacity` (`contract.capacity`); a bonus offer must be inside its window and not consumed (`contract.bonus.unavailable`) | Fixes `requiredUnits`. For a production objective it sets `Produced = 0`. `AutoDeliver = false`. |
| `DeliverContract()` (changed) | delivery objective only (`command.invalid` for a production objective, which the snapshot never offers); F003 checks | Shared `ContractRewards.Grant`. Starts the offer cooldown. |
| `AbandonContract()` (changed) | F003 checks | Clears the active contract and starts the offer cooldown (FR-005). |
| `SetAutoDeliver(bool)` | an active delivery-objective contract (`contract.none-active`; `command.invalid` for a production objective); for `true`, the `DeliverContract` worst-case pre-check (`ContractRewards.CheckGrant` plus `SettleFits` with the reward Cash and the consumed stock), else `value.limit` | Sets the flag, then settles. If the stock is present, the settle delivers at t = 0. |
| `ConfirmPrestige()` (changed) | `FV ≥ PrestigeThreshold.For(min(PrestigeCount, MaxIndex))` (`prestige.below-threshold`) | F003 transition. The candidate has `PrestigeCount + 1`. |

Simulator-driven outcomes (no command):

- **Auto-delivery**: at the first tick before the deadline boundary event with `stock ≥ requiredUnits`. Simulation order decides; there is no real-time compare. Within the settle loop the order is completions → auto-delivery → auto-sell with a reserve (research F5-R05). An overflow records `AutoDeliverBlocked(value.limit)` and retries at the next instant.
- **Production completion**: at `Produced ≥ requiredUnits`.
- **Expiry**: grants nothing and starts the cooldown.
- **Bonus**: the unlock pass triggers it and stores the window end as a UTC stamp. Cooldown ends and bonus window ends are **not** boundary events, and snapshots and `AcceptContract` evaluate them lazily against the timeline.
- **Reward Cash** (all paths): `MulDivFloor(SalePricing.Value(required, basePrice, salePricePpm), rewardRatioPpm, 10^6)`.
- **Offline summary**: gains `AutoDelivered`, `ProduceCompleted`, `Expired` and reward totals.

All of these appear in `ContractsSnapshot.LastOutcome`, a value type, and in the session-log events.

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

Work unit 2a moves this table to the stable path `docs/contracts/run-reason-codes.md` (AR-16). That file is then authoritative and pinned by `ReasonCodeContractTests` (#355): set equality with the code constants, and every rejection code has player copy.

| Code | Source | Since |
|---|---|---|
| `slot.occupied` | RunReasons | F003 |
| `slot.unavailable` | RunReasons | F003 |
| `machine.locked` | RunReasons | F003 |
| `cash.insufficient` | RunReasons | F003 |
| `level.max` | RunReasons | F003 |
| `confirmation.required` | RunReasons | F003 |
| `priority.not-permutation` | RunReasons | F003 |
| `resource.locked` | RunReasons | F003 |
| `contract.active-exists` | RunReasons | F003 |
| `contract.none-active` | RunReasons | F003 |
| `contract.expired` | RunReasons | F003 |
| `stock.insufficient` | RunReasons | F003 |
| `item.none` | RunReasons | F003 |
| `item.target.invalid` | RunReasons | F003 |
| `effect.conflict` | RunReasons | F003 |
| `research.prerequisite` | RunReasons | F003 |
| `rp.insufficient` | RunReasons | F003 |
| `overdrive.cooling-down` | RunReasons | F003 |
| `overdrive.active` | RunReasons | F003 |
| `ip.insufficient` | RunReasons | F003 |
| `prestige.below-threshold` | RunReasons | F003 |
| `transition.pending` | RunReasons | F003 |
| `value.limit` | RunReasons | F003 |
| `lifecycle.blocked` | RunReasons | F003 |
| `command.invalid` | RunReasons | F003 (added to the contract by #355) |
| `checkpoint.content` | RunPersistence | F003 (added by #355) |
| `discard.unavailable` | RunPersistence | F003 (added by #355) |
| `transition.committed` | RunPersistence | F003 |
| `persistence.failed` | RunPersistence | F003 |
| `return.blocked` | RunPersistence | F003 |
| `lifecycle.noop` | RunPersistence | F003 |
| `checkpoint.none` | RunPersistence | F003 |
| `checkpoint.invalid` | RunPersistence | F003 |
| `checkpoint.version` | RunPersistence | F003 |
| `offline.value-limit` | RunOfflineSummary (summary code) | F003 |
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
