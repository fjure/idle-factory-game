# Data Model: Economy and Contracts (Feature 005)

Changes relative to the F003 data model (`specs/003-first-factory-prestige/data-model.md`). All numbers are `long` values in [0, 10^18] with checked arithmetic. ppm means parts per million.

## Content (schema v3, immutable after catalog build)

| Entity | New or changed fields | Validation |
|---|---|---|
| `RunStartDef` | `SellSteps` (1–8 positive integers, ascending, plus an optional `All`); `LiveGapThresholdMs` (≥ 1,000); `ManualMining { ResourceId, BaseYieldMilli ≥ 1, MaxActivationsPerSecond (1–20), StartWindowMs, ReferenceMinerLevel, MaxShareOfMinerPpm ≤ 3e6, MiningAdvantageMaxPpm }` (D-O, D-P); `MinRunDurationMs`, `UptimeMaxPpm` (ladder) | `sell.steps`, `live-gap.range`, `manual.yield.bound`, `start.reachable` |
| `ContractOfferDef` | Fields: `Objective` (`deliver` \| `produce`), `DurationMs?`, `Class` (`standing` \| `bonus`), `RewardRatioPpm`, `CooldownMs` (≥ 0), `Bonus? { Trigger: UnlockCondition, WindowMs }`, `QuantityCurve` (unchanged), RP reward curve (unchanged). The `Kind` vocabulary is removed. | `contract.ratio.standing` (R in [1,000,000, 1,500,000]); `contract.ratio.bonus` (≤ 2,000,000); `contract.ratio.produce` (0 < R and R + max standing delivery R for the resource ≤ 2,000,000); `contract.quantity.cap`; `contract.produce.min`; `contract.bonus.fields` (Bonus is present exactly when Class = bonus); `contract.quantity.capacity` (deliver only) |
| `ContractItemLadder` (catalog) | `Indices[]` (ascending, < 1024); `PerRunCap`; `ItemIds[]` per index | `ladder.uptime` (ECO-6 against Item durations) |
| `MachineDef` | `BuildPrice { Base, OwnedGrowthPpm ≥ 10^6, PrestigeGrowthPpm ≥ 10^6, Cap }`; `Cost` keeps meaning the upgrade cost | `build.price.cap`; first-Miner check moved to `start.reachable` (manual mining, F5-R19; formerly the first Miner from starting Cash at every prestige index up to `MaxIndex`, after the maximum discounts) |
| `PrestigeDef` | `Threshold { Base, GrowthPpm ≥ 10^6, MaxIndex ≤ 10,000 }` (index clamped at `MaxIndex`, D-K; the `MaxIndex` bound is owner decision 2026-10-13-b, #396); `IpCurve` unchanged (`sqrt`, coefficient 10, divisor 10,000,000) | `prestige.threshold.bound` (T(MaxIndex) ≤ 10^18) |
| `MilestoneDef[]` | `FractionPpm` in (0, 10^6), strictly ascending | `milestone.order` |
| `IndustryUpgradeDef` | `DecayPpm` (≤ 10^6); `PracticalCeilingLevel`; `MinGainPpm`; precomputed `ValueTable` | `industry.ceiling.level` |
| Catalog | `ChainDominanceMarginPpm` (> 0) | `chain.dominance` (static output per slot-second, chain against Ore-only, at level 1 and at every milestone level) |

## Run state (mutable; sole writer `RunCommands` and `RunSimulator`)

| Entity | New fields | Owner | Persisted (Save V3) | Prestige |
|---|---|---|---|---|
| `ActiveContract` | `AutoDeliver` (bool); `Produced` (long, produce objective only) | `ContractBoard` | yes | reset |
| `ContractCooldowns` | `offerId → readyAtUtcMs` | `ContractBoard` | yes | reset |
| `BonusOfferState` | `offerId → { Triggered, WindowEndUtcMs, Consumed }` | `ContractBoard` (the unlock pass triggers it) | yes | reset |
| `ItemLadderCount` | Ladder index = run-wide total of completed contracts (all offers); an entry is granted when the pre-increment run-wide index equals the entry index (ECO-11), up to `PerRunCap` (interim reading: total entries per run, owner decision open, #387) | `ContractBoard` | `runCompletionTotal` (Save V3, SEC-8); a V2 load derives it from the counts | reset |
| `ContractBoard.Version` | Incremented on every contract-state change; part of the session publish tokens | `ContractBoard` | no | reset |
| Mining rate counter and remainder | `(TimelineSecond, CountedInSecond, RemainderMilli)`; in code the internal `RunState.MiningTimelineSecond`, `MiningCountedInSecond`, `MiningRemainderMilli` (#385, 2026-10-14) | `RunState` (commands only) | no (transient; fresh after load, so at most one extra second of the cap after a load, MIN-7) | reset |
| `PermanentState.PrestigeCount` | unchanged; now also the threshold and build-price index | `PrestigeTransition` | yes (existing) | +1, exactly once |

Expired contract snapshot (T340): `ExpiredContractSnapshot.AutoDeliverWasOn` is captured from the persisted `activeContract.autoDeliver` at the expiry; `LastExpired` itself is not persisted (no Save V3 member).

Derived, not persisted: build price (`BuildPricing`), threshold (`PrestigeThreshold`), milestones (`GoalsSnapshot`), bottleneck seconds per resource (the session's blocked-machine meter, see Session log › Bottleneck), the practical Industry ceiling.

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
| Header | `schema`, `contentRevision`, `buildKind` (release, development, editor), `platformClass` (desktop, mobile), `timeScale`, `sessionId` (128 random bits, 32 hex digits, per app start). `timeScale` is a snapshot at session start (A3): the DevTools display factor as a whole number, clamped to [1, 2^31-1], 1 in real time. A development build whose factor changes mid-session appends one more Header of the same `sessionId` (D-I). The writer also appends a Header after Clear, and when recording is switched on in a session that has none |
| Sample | `playSeconds` (the timeline seconds since the session's first sight of the run, so a run resumed from a save counts from the resume), `factoryValue`, `cash`, machines by type and level (counts), `storageLevel`, `factoryLevel` |
| Milestone | `kind` (firstGear, firstContract, firstItem, firstPrestige, run2Eligible), `playSeconds` |
| Sale | `resourceId`, `units`, `cash`, `manual` (bool), aggregated per resource and minute |
| Mining | `resourceId`, `counted`, `ignored` (over the rate cap), `blocked` (full store or numeric bound), `ore`; one record per `Mine` call including rejected calls, rolled up per resource and minute by the adapter (#385, 2026-10-14) |
| Contract | `event` (accept, deliver, autoDeliver, produceComplete, expire, abandon, blocked; `blocked` is raised once when the completion becomes value.limit-blocked, with `reward` 0, #392), `offerId`, `index`, `required`, `reward`, `ratioPpm` |
| Prestige | `factoryValue`, `threshold`, `ip`, `secondsSinceEligible` (timeline seconds from the goal line's first sight of the Factory Sale in the run, or the confirmation if it never saw it, to the confirmation), `industryPurchases[]` (the permanent levels after the prestige). Raised once when the pending transition is published, never at the confirmation or at a cancel (T364) |
| OfflineReturn | `absenceMs`, `creditedMs`, `capHit`, `completions`, `storeFullStallSeconds` (0 until the summary measures it), `soldUnits`, `cashDelta`, `reclassifiedLiveGap`. The offline outcomes of the return (autoDeliver, produceComplete, expire) are written as Contract records by the offline adapter from the summary (W5, #392) |
| Bottleneck | `resourceId`, `missingInputSeconds`, `storeFullSeconds` (machine-seconds per sample window: a machine that cannot run a cycle (missing input or full store) after two consecutive live advance steps, attributed to the resource it produces; offline absences are not measured, T364) |
| Purchase | `kind`, `targetId`, `level` (the level reached), `cost` (Cash, RP for research, IP for industry), `secondsSinceAffordable` (whole seconds from the first observation at or above the cost, after the last one below it, to the purchase; measured from the session's own observations, so at most the session's age, T364) |
| Clock | `kind` (backward, forwardJump, liveGap), `deltaMs`, `reclassified`. `liveGap`: the wall delta of the gap frame, `reclassified` true when the standard offline return took it. `forwardJump`: a live frame whose UTC delta exceeds the real delta by more than 5,000 ms (surplus in `deltaMs`, `reclassified` false). `backward`: an offline return with a clock before the departure (T364) |
| Lifecycle | `event` (start, quit, focusLossCheckpoint, logToggled, cleared), `droppedEvents` (the session guard's count plus the writer's own drops) |

Notes on the derived records (T364):

- **Bottleneck source and caller.** `RunSession.RaiseBottlenecks()` is public Core API; the Unity adapter is its only caller (with each Sample, and at the focus-loss and quit lifecycle points). It reads the machine read model after every live `Advance`, and only while the sink is recording (`IRunLogRecordingState`, implemented by the writer); with recording off there is no per-advance work.
- **Attribution.** A blocked machine counts on `Outputs[0]` of its recipe. Every shipped recipe has exactly one output, so this is exact today; a future multi-output recipe attributes to its first output.
- **Lag and partition dependence.** A block is counted from the step after the one that first observed it (two consecutive blocked steps), so seconds depend on how live time is cut into advance steps, by at most one step per block. This never affects the simulation. Offline absences are not measured (`storeFullStallSeconds` is 0), so Bottleneck seconds, and with them the SC-005 ratios, are lower bounds across returns.
- **Loads and returns.** An ordinary return or load of the same run raises the open Bottleneck window first, then keeps the Factory Sale eligibility and the affordability history, so `secondsSinceEligible` and `secondsSinceAffordable` count across it; only the blocked-step memory restarts. A different run that is not a prestige successor, and a prestige successor, reset the meters.
- **Clock.** The `forwardJump` threshold (`FactoryRuntimeHost.ForwardJumpMinMs`, 5,000 ms) sits next to the live-gap threshold (`RunPersistence.LiveGapThresholdMs`); a frame without a previous UTC reference (after an unreadable clock) records neither.

IDs are content IDs only. No text, paths, exceptions or save bytes are recorded.
