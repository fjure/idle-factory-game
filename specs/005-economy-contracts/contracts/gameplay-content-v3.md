# Gameplay content contract: schema version 3

This contract replaces the v2 contract (`specs/003-first-factory-prestige/contracts/gameplay-content-v2.md`) for gameplay content. Everything not listed here keeps the v2 shape and rules: the strict token reader, collected diagnostics, all-or-nothing build and the canonical form. The v1 legacy catalog is unchanged.

- `contentSchemaVersion` is `3`. The dispatcher keeps its code `content.version.unsupported` for any other version. The reader evolves in place. Both shipped copies (the packaged file and the PlayMode fixture copy) and the schema file are migrated in one unit.
- The shipped v3 content keeps the F003 offer IDs `contract.plate`, `contract.gear` and `contract.timed-bar`.
- `contentRevision` is `3` for the first F005 tuning. It is owner-approved per FR-027.

## Member changes

```text
runStart:
  + sellSteps: [int ≥1 ascending, 1..8 entries] + optional "all"
  + liveGapThresholdMs: int ≥ 1000          (default content value 60000)
  + manualMining: { resourceId, baseYieldMilli ≥ 1, maxActivationsPerSecond 1..20, startWindowMs, referenceMinerLevel ≥ 1, maxShareOfMinerPpm ≤ 3e6, miningAdvantageMaxPpm }
  effect kinds: + manualYield (multiplicative ppm; Research and Industry)
  + minRunDurationMs, uptimeMaxPpm          (ladder uptime rule)
machines[]:
  + buildPrice: { base, ownedGrowthPpm ≥ 1e6, prestigeGrowthPpm ≥ 1e6, cap }
contracts[]:
  - kind                                     (removed)
  + objective: "deliver" | "produce"
  + class: "standing" | "bonus"
  + rewardRatioPpm: int
  + cooldownMs: int ≥ 0
  + bonus: { trigger: UnlockCondition, windowMs ≥ 1 }   (exactly when class = bonus)
    durationMs (optional: timed), quantity curve, rp reward curve, unlock   (unchanged)
  - cash reward curve and item rewards       (removed: cash from rewardRatioPpm, items from the ladder)
+ contractItemLadder: { entries: [{ index, itemId }], perRunCap }
prestige:
  - thresholdFactoryValue                    (removed)
  + threshold: { base, growthPpm ≥ 1e6, maxIndex ≥ 1 }
    ipCurve                                  (unchanged)
+ milestones: [{ fractionPpm }]              (strictly ascending, each in (0, 1e6))
industryUpgrades[]:
  + decayPpm ≤ 1e6, practicalCeilingLevel, minGainPpm
+ chainDominanceMarginPpm > 0
```

## Rule codes (new)

| Code | Rule |
|---|---|
| `sell.steps` | Steps are 1–8 distinct ascending positive integers, plus an optional `all`. |
| `live-gap.range` | 1,000 ≤ `liveGapThresholdMs` ≤ 28,800,000 (8 h). |
| `contract.ratio.standing` | Standing delivery offers: 1.0 ≤ R ≤ 1.5. |
| `contract.ratio.bonus` | Bonus offers: R ≤ 2.0 (and R ≥ 1.0 for delivery). |
| `contract.ratio.produce` | Production objectives: 0 < R, and R + the highest standing delivery R for the same resource ≤ 2.0. |
| `contract.quantity.cap` | Delivery offers: `quantity.cap` ≤ the top storage capacity. |
| `contract.produce.min` | Production objectives: minimum required quantity (content constant). |
| `cost.floor` | Combined build and upgrade cost multiplier ≥ the content floor (initial 0.5×). |
| `speed.level.dead` | No purchasable level has zero effect after floors. |
| `contract.bonus.fields` | `bonus` is present exactly for `class = bonus`. |
| `contract.quantity.capacity` | Delivery offers: required ≤ the highest storage capacity, for every reachable index < 1024. |
| `ladder.uptime` | Ladder Items within the per-run cap cannot exceed the ECO-6 uptime bound for their durations. |
| `build.base.bound` | `base ≤ 10^12` and `cap ≤ 10^12` (structural). |
| `build.price.cap` | `cap ≥ base`. |
| `start.reachable` | Starting Cash plus the sale of Ore mined by hand at the cap within `startWindowMs` covers the first Miner (owned 0, no discounts) at every prestige index ≤ `maxIndex`. |
| `manual.yield.bound` | max mining Ore/s (`baseYieldMilli` × max Research × max Industry `manualYield` ÷ 1000 × cap) ≤ `maxShareOfMinerPpm` × Miner output per second at `referenceMinerLevel` with max Industry speed; every purchasable `manualYield` level changes the effective milli-yield. |
| `prestige.threshold.bound` | T(maxIndex) ≤ 10^18 with checked math. |
| `milestone.order` | Milestones strictly ascend within (0, 1e6). |
| `industry.ceiling.level` | `practicalCeilingLevel` is the first level whose marginal value is below `minGainPpm`. |
| `chain.dominance` | Static output per slot-second: chain > Ore-only × (1 + margin), at base level and at each milestone level. |

Revision gating: `contract.ratio.standing`, `contract.ratio.bonus`, `contract.ratio.produce`, `chain.dominance`, `ladder.uptime`, `start.reachable` and `manual.yield.bound` apply to `contentRevision ≥ 3`. Step 4b ships schema v3 with revision 2 values.

Rule ownership: the structural rules land with the content v3 unit. `contract.quantity.capacity` and `ladder.uptime` land with contracts, `build.price.cap` with build pricing, `start.reachable` and `manual.yield.bound` with manual mining, `prestige.threshold.bound` and `industry.ceiling.level` with threshold and Industry, and `chain.dominance` with sale pricing.

The removed members (`kind`, `thresholdFactoryValue`, cash reward curves, item rewards on offers) produce `member.unknown` if present.

Member names and notes (T313):

- The RP reward member of an offer is `rpReward`: a curve, required (base 0 means no RP). The v2 `rewards` list does not exist in v3.
- The `all` sell step is the string `"all"` inside `sellSteps` and may only be the last entry; every other entry is an integer.
- Retired v2 rule codes: `contract.kind.unknown`, `contract.kind.resource-mismatch`, `contract.kinds.incomplete`, `contract.reward.empty`, `contract.reward.kind.unknown`, `contract.duration.required` and `contract.duration.forbidden`. Their members (`kind`, `rewards`, the timed-only `durationMs` rule) are gone, so a document that still carries them fails with `member.unknown`. `durationMs` is optional, and a present value makes the offer timed.
- Added with the reader: `contract.objective.unknown` and `contract.class.unknown` (closed vocabularies, checked before a contract offer definition is built).

Structural rules (T314), decisions:

- `contract.ratio.standing` and `contract.ratio.bonus` (revision 3 and later) layer on top of the v2 `contract.reward.ratio`, which stays ungated (floor R ≥ 1.0 for delivery, ceiling `maxRewardRatioPpm`, adapter overflow) so that revision 2 content keeps validating. They run only for an offer the v2 check did not report. `contract.ratio.standing` is the 1.5 ceiling of a standing delivery offer, `contract.ratio.bonus` the 2.0 ceiling of a bonus offer. `contract.ratio.produce` stays with T320.
- `contract.quantity.cap`, `contract.produce.min` and `cost.floor` have no catalog member. They use validator constants: the top storage capacity (the largest `storageLevels` capacity), `ProduceMinQuantity` = 10 and `CostFloorPpm` = 500,000 (0.5x). `contract.quantity.cap` checks index 0 of the delivery quantity curve against the top storage capacity; every reachable index is `contract.quantity.capacity` (T320). `contract.produce.min` checks index 0 of a production offer. `cost.floor` folds the `upgradeCost` factors of Research and Industry at their maximum levels per machine and for the expansion track.
- `speed.level.dead` also covers an Industry `startingStorageLevel` upgrade whose last level adds nothing (`1 + valuePerLevel × (maxLevel − 1)` ≥ number of `storageLevels`).
- `chainDominanceMarginPpm` must be greater than 0. The ladder's ascending index order is not a document-order rule: the validator's duplicate check is order-insensitive, and `RunCatalogBuilder` keeps the document order of the entries today. Sorting by (index, itemId) is left to T320.
