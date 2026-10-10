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
  + startingCashPrestigeGrowthPpm: int ≥ 1e6
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
| `build.start.affordable` | The first Miner is affordable from starting Cash at every prestige index ≤ `maxIndex`. |
| `prestige.threshold.bound` | T(maxIndex) ≤ 10^18 with checked math. |
| `milestone.order` | Milestones strictly ascend within (0, 1e6). |
| `industry.ceiling.level` | `practicalCeilingLevel` is the first level whose marginal value is below `minGainPpm`. |
| `chain.dominance` | Static output per slot-second: chain > Ore-only × (1 + margin), at base level and at each milestone level. |

Revision gating: `contract.ratio.standing`, `contract.ratio.produce`, `chain.dominance`, `ladder.uptime` and `build.start.affordable` apply to `contentRevision ≥ 3`. Step 4b ships schema v3 with revision 2 values.

Rule ownership: the structural rules land with the content v3 unit. `contract.quantity.capacity` and `ladder.uptime` land with contracts, `build.price.cap` and `build.start.affordable` with build pricing, `prestige.threshold.bound` and `industry.ceiling.level` with threshold and Industry, and `chain.dominance` with sale pricing.

The removed members (`kind`, `thresholdFactoryValue`, cash reward curves, item rewards on offers) produce `member.unknown` if present.
