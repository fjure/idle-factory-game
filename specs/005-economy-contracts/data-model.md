# Data Model: Economy and Contracts (Feature 005)

Changes relative to the F003 data model (`specs/003-first-factory-prestige/data-model.md`). All numbers are `long` values in [0, 10^18] with checked arithmetic. ppm means parts per million.

## Content (schema v3, immutable after catalog build)

| Entity | New or changed fields | Validation |
|---|---|---|
| `RunStartDef` | `SellSteps` (1–8 positive integers, ascending, plus an optional `All`); `LiveGapThresholdMs` (≥ 1,000); `StartingCashPrestigeGrowthPpm` (≥ 10^6, D-J); `MinRunDurationMs`, `UptimeMaxPpm` (ladder) | `sell.steps`, `live-gap.range`, `build.start.affordable` |
| `ContractOfferDef` | Fields: `Objective` (`deliver` \| `produce`), `DurationMs?`, `Class` (`standing` \| `bonus`), `RewardRatioPpm`, `CooldownMs` (≥ 0), `Bonus? { Trigger: UnlockCondition, WindowMs }`, `QuantityCurve` (unchanged), RP reward curve (unchanged). The `Kind` vocabulary is removed. | `contract.ratio.standing` (R in [1,000,000, 1,500,000]); `contract.ratio.bonus` (≤ 2,000,000); `contract.ratio.produce` (0 < R and R + max standing delivery R for the resource ≤ 2,000,000); `contract.quantity.cap`; `contract.produce.min`; `contract.bonus.fields` (Bonus is present exactly when Class = bonus); `contract.quantity.capacity` (deliver only) |
| `ContractItemLadder` (catalog) | `Indices[]` (ascending, < 1024); `PerRunCap`; `ItemIds[]` per index | `ladder.uptime` (ECO-6 against Item durations) |
| `MachineDef` | `BuildPrice { Base, OwnedGrowthPpm ≥ 10^6, PrestigeGrowthPpm ≥ 10^6, Cap }`; `Cost` keeps meaning the upgrade cost | `build.price.cap`; `build.start.affordable` (the first Miner from starting Cash at every prestige index up to `MaxIndex`, after the maximum discounts) |
| `PrestigeDef` | `Threshold { Base, GrowthPpm ≥ 10^6, MaxIndex }` (index clamped at `MaxIndex`, D-K); `IpCurve` unchanged (`sqrt`, coefficient 10, divisor 10,000,000) | `prestige.threshold.bound` (T(MaxIndex) ≤ 10^18) |
| `MilestoneDef[]` | `FractionPpm` in (0, 10^6), strictly ascending | `milestone.order` |
| `IndustryUpgradeDef` | `DecayPpm` (≤ 10^6); `PracticalCeilingLevel`; `MinGainPpm`; precomputed `ValueTable` | `industry.ceiling.level` |
| Catalog | `ChainDominanceMarginPpm` (> 0) | `chain.dominance` (static output per slot-second, chain against Ore-only, at level 1 and at every milestone level) |

## Run state (mutable; sole writer `RunCommands` and `RunSimulator`)

| Entity | New fields | Owner | Persisted (Save V3) | Prestige |
|---|---|---|---|---|
| `ActiveContract` | `AutoDeliver` (bool); `Produced` (long, produce objective only) | `ContractBoard` | yes | reset |
| `ContractCooldowns` | `offerId → readyAtUtcMs` | `ContractBoard` | yes | reset |
| `BonusOfferState` | `offerId → { Triggered, WindowEndUtcMs, Consumed }` | `ContractBoard` (the unlock pass triggers it) | yes | reset |
| `ItemLadderCount` | Ladder index = run-wide total of completed contracts (all offers); grants = ladder entries with index < total, capped at `PerRunCap` | `ContractBoard` | `runCompletionTotal` (Save V3, SEC-8); a V2 load derives it from the counts | reset |
| `ContractBoard.Version` | Incremented on every contract-state change; part of the session publish tokens | `ContractBoard` | no | reset |
| `PermanentState.PrestigeCount` | unchanged; now also the threshold and build-price index | `PrestigeTransition` | yes (existing) | +1, exactly once |

Derived, not persisted: build price (`BuildPricing`), threshold (`PrestigeThreshold`), milestones (`GoalsSnapshot`), bottleneck seconds per resource (the simulator's wait counters), the practical Industry ceiling.

## State transitions: contract offer

```text
Locked --unlock--> Available --accept--> Active --deliver|auto-deliver|produce-complete--> Cooldown
                                      Active --abandon (untimed)--> Cooldown
                                      Active --deadline passed--> Cooldown (expired, no reward)
Cooldown --readyAt reached--> Available
Bonus: Locked --trigger--> Available(window) --accept--> Active --end--> Consumed
       Available(window) --window end--> Consumed
Any --prestige--> initial state of the next run
```

The auto-deliver flag lives only on `Active` and clears on every exit.

## Session log (not game state; Unity adapter)

| Record | Fields (allowlist) |
|---|---|
| Header | `schema`, `contentRevision`, `buildKind` (release, development, editor), `platformClass` (desktop, mobile), `timeScale`, `sessionId` (random per app start) |
| Sample | `playSeconds`, `factoryValue`, `cash`, machines by type and level (counts), `storageLevel`, `factoryLevel` |
| Milestone | `kind` (firstGear, firstContract, firstItem, firstPrestige, run2Eligible), `playSeconds` |
| Sale | `resourceId`, `units`, `cash`, `manual` (bool), aggregated per resource and minute |
| Contract | `event` (accept, deliver, autoDeliver, produceComplete, expire, abandon), `offerId`, `index`, `required`, `reward`, `ratioPpm` |
| Prestige | `factoryValue`, `threshold`, `ip`, `secondsSinceEligible`, `industryPurchases[]` |
| OfflineReturn | `absenceMs`, `creditedMs`, `capHit`, `completions`, `storeFullStallSeconds`, `soldUnits`, `cashDelta`, `reclassifiedLiveGap` |
| Bottleneck | `resourceId`, `missingInputSeconds`, `storeFullSeconds` (per sample window) |
| Purchase | `kind`, `targetId`, `level`, `cost`, `secondsSinceAffordable` |
| Clock | `kind` (backward, forwardJump, liveGap), `deltaMs`, `reclassified` |
| Lifecycle | `event` (start, quit, focusLossCheckpoint, logToggled, cleared), `droppedEvents` |

IDs are content IDs only. No text, paths, exceptions or save bytes are recorded.
