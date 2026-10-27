# Content record r2: Iron factory content sheet, revision 2 (balance iteration)

**Task**: T125 (GitHub Issue #207), Game Systems/Economy Designer, Game Designer/Product Owner lens applied to the proposal. Review lenses still to come: independent Product and Economy verdict (T132), human playtest (T126), owner approval (T127).
**Document**: `Assets/IdleFactory/Content/Iron/iron-factory-content.json`, `contentSchemaVersion` 2, `contentRevision 2` (revision 1 is kept as history in `content-record-r1.md`, `balance-trace-r1.md` and `Fixtures/Balance/trace-r1.csv`).
**Status**: a **balance hypothesis**, a *proposal* for the owner gate. Only the FR-002, FR-007, FR-012 and FR-021 targets (and the FR-008, FR-010, FR-018, FR-019 numbers the spec names) are authoritative; everything this record changes was chosen by T125 from the trace and is unapproved until the human playtest (T126) and the owner approval of this `contentRevision` (T127, R13). Nothing here is a promise about pacing. The trace behind it is `balance-trace-r2.md`.
**Evidence**: every number below is recomputed from the shipped document by `RunContentCanonicalTests` (worst-case speed and batch, sale-price stock bound, cash ratio range) and by `BalanceTraceTests` (the trace, the partial-batch statistics quoted in section 4), and the document passes `RunContentValidator`.

## 0. What revision 2 changes against revision 1

Changes are listed with the evidence in `balance-trace-r2.md` section 3 (one-at-a-time reversal of each change against the shipped revision 2). The spec-fixed values (recipes FR-002, market FR-012, storage table FR-007, slot counts FR-008, curve growth and milestones FR-010, Overdrive FR-019, prestige FR-021, offline policy revision 1) are **not** touched.

| Field | r1 | r2 | Reason | Evidence |
|---|---|---|---|---|
| `runStart.startingCash` | 25 | 37 | The Miner now costs $30; start Cash must buy the first Miner, or the first run owns no machine (trace: "not reached" at $25) | reversal row 1; `StartingCashBuysAtLeastTheFirstMiner...` |
| Miner `cost.base` | 10 | 30 | Ore-only dominance: a Miner-only policy reached $10M at 3,360 s against "not reached" for the chain (`balance-trace-r1.md` section 3). At $30 the same probe needs 6,020 s | probe row in `balance-trace-r2.md` section 2 |
| Smelter `cost.base` | 40 | 9 | The chain is the economy that pays 3x per ore; reversing this one value alone delays the first prestige to 14,720 s (the Smelter gates every later machine) | reversal: first prestige 14,720 s instead of 6,940 s |
| Press `cost.base` | 150 | 21 | same | reversal: 11,260 s |
| Assembler `cost.base` | 600 | 78 | The first Gear waited for $600 plus factory level 3 at minute 20; at $78 it is built before minute 12 | reversal: not reached |
| `factoryLevels` 2, 3, 4 `cost` | 800 / 3,000 / 12,000 | 320 / 1,900 / 8,500 | Gear and Assembler unlock at factory level 3; r1 needed $3,000 for it | reversal of all three: first Gear 1,060 s instead of 720 s |
| `factoryLevels` 5 `cost` | 48,000 | 48,000 | unchanged | |
| `contract.gear` cash base | 3,840 | 4,000 | cash ratio 1.60 -> 1.67 of market value (limit 2.00) | reversal of both contract rewards: first prestige 6,720 s |
| `contract.plate` cash base | 1,280 | 1,400 | cash ratio 1.60 -> 1.75 | same |
| `industry.calibration` | x1.03 per level, 10 levels, 8 IP | x1.22 per level, 3 levels, 1 IP | r1 gave +3 % for 8 IP: run 2 saved 1 % of its time (scaled scenario of r1: 2,800 s against 2,820 s) | reversal: second run 6,340 s instead of 4,640 s |
| `industry.market-contacts` | x1.02, 10 levels, 10 IP | x1.19, 2 levels, 1 IP | same | reversal: 5,020 s |
| `industry.toolmaker` | x0.98, 5 levels, 12 IP | x0.639, 1 level, 3 IP | Industry cost factor stays at or above x0.5 (canonical test) | reversal: run 2 unchanged (the reference policy first buys it after run 2), runs 4 and 5 slower |
| `industry.seed-capital` | +$500, 5 IP | +$300, 10 IP | the reference policy buys it second in its Industry order and its Cash applies one run later; at 5 IP it consumed the IP that run 2 needed | reversal: runs 3 to 5 become 4,320 / 4,860 / 5,140 s, ECO-3 fails |
| `industry.warehouse` | 20 IP | 20 IP | unchanged | |

Research, Items, Overdrive, storage, the `timed-bar` contract, the RP curves, and every ID, label and presentation key are unchanged. Values moved by more than 20 % are all in the list above; because the trace missed the bands by a large margin they could not be tuned in steps of 20 %. The owner approves them as one revision (T127).

## 1. Values fixed by the spec versus values chosen by this sheet

| Area | Value | Source |
|---|---|---|
| Recipes | Miner 2 Ore / 2 s (no input); Smelter 2 Ore -> 1 Bar / 2 s; Press 2 Bars -> 1 Plate / 4 s; Assembler 2 Plates -> 1 Gear / 5 s | FR-002 (spec). One Press supplies 5/8 = 62.5 % of one Assembler's Plate demand (derived by the test from the recipes) |
| Market | Ore $1, Bar $3, Plate $8, Gear $24 | FR-012 (spec) |
| Storage | 1,000 / 2,000 / 4,000 / 8,000 / 16,000 at $0 / 2,500 / 10,000 / 40,000 / 160,000 | FR-007 (spec) |
| Factory slots | 4 / 6 / 8 / 10 / 12 at levels 1 to 5 | FR-008 (spec) |
| Factory costs | $0 / 320 / 1,900 / 8,500 / 48,000 | **chosen** (r2) |
| Machine curves | cost growth 1,150,000 ppm (ceil), speed growth 1,080,000 ppm, milestones x2 / x2 / x3 / x5 at levels 10 / 25 / 50 / 100, `maxLevel` 100 | FR-010 (spec: "near"); `maxLevel` 100 is **chosen** |
| Machine base costs | Miner $30, Smelter $9, Press $21, Assembler $78 | **chosen** (r2) |
| Start | `startingCash` $37 (the first Miner and $7), `initialUnlocks` Iron Ore and the Miner, `removalRefundPpm` 500,000 | refund per spec task text; cash **chosen** (r2) |
| Run bounds | `minCycleTicks` 1,000,000 (0.1 s), `maxCombinedSpeedPpm` 10^12 (the contract maximum), `maxCombinedOutputFactor` 4, `maxRewardRatioPpm` 2,000,000 | **chosen** (OD-6, GD-5) |
| Overdrive | x2 for 30 s, five-minute cooldown, unlocked at factory level 2 | FR-019 (spec); unlock **chosen** |
| Prestige | threshold $10,000,000, IP = floor(10 x sqrt(FV / 10^7)) | FR-021 (spec): 10 IP at $10M, 20 at $40M, 30 at $90M, 100 at $1B (asserted) |
| Offline policy | revision 1, 4/5, 8 h | locked (contract) |

### Contracts, Items, Research, Industry

| Contract | Kind | Resource | Quantity (completion 0, growth) | Cash reward (completion 0) | Duration | Item grants (completion indices) | RP per completion |
|---|---|---|---|---|---|---|---|
| `contract.gear` | gear | `iron.gear` | 100, x1.01 (ceil) | $4,000, x1.01 (ceil) | none | `item.overclock-core` at [1, 5, 10] | 8 flat |
| `contract.plate` | plate | `iron.plate` | 100, x1.01 (ceil) | $1,400, x1.01 (ceil) | none | `item.flux-catalyst` at [1, 4, 9] | 4 flat |
| `contract.timed-bar` | timedBar | `iron.bar` | 100, x1.01 (ceil) | $510, x1.01 (ceil) | 240 s | `item.diamond-drill-head` at [0, 3, 6, 10] | 2 flat |

The three Items, nine Research entries and Overdrive are unchanged from revision 1 (see `content-record-r1.md` section 1).

| Industry upgrade | Effect (per level) | maxLevel | IP cost (level 1, growth) | Cumulative at the cap |
|---|---|---|---|---|
| `industry.calibration` | machineSpeed allMachines x1.22 | 3 | 1, x1.50 (1 + 2 + 3 IP) | x1.8158 speed |
| `industry.market-contacts` | salePrice allResources x1.19 | 2 | 1, x1.50 (1 + 2 IP) | x1.4161 price |
| `industry.seed-capital` | startingCash +300 | 10 | 10, x1.50 | +$3,000 |
| `industry.toolmaker` | upgradeCost allMachines x0.639 | 1 | 3, x1.60 | x0.639 machine cost |
| `industry.warehouse` | startingStorageLevel +1 | 2 | 20, x2.00 | +2 levels |

The first IP award at the threshold is 10, which affords every level of Calibration (1 + 2 + 3 IP) and of Market Contacts (1 + 2 IP): the first prestige buys all five levels (9 IP) and leaves 1 IP. The caps are the canonical-test caps of revision 1 (speed at most x2, price at most x2, cost at least x0.5, starting Cash at most 1 % of the threshold) and are unchanged: r2 moves the *front* of the curve (strong first levels, few of them), not the ceiling.

## 2. Theoretical maximum income per second

Assumptions as in revision 1: every machine at level 100, every speed source at its maximum (Research, Industry, the best Item per conflict key, Overdrive active), every sale-price source at its maximum, output factor 4 on the Miner. This is a ceiling, not an expectation: a Miner reaching level 100 costs $267,539,272 in total (revision 1: $102,277,099), more than the whole run is worth.

| Machine | Executions per second per machine (worst-case speed / recipe duration) | Units produced per second | Units consumed per second |
|---|---|---|---|
| Miner | 339,866.29 | 2,718,930 | 0 |
| Smelter | 475,812.81 | 475,813 | 951,626 |
| Press | 237,906.41 | 237,906 | 475,813 |
| Assembler | 135,946.52 | 135,947 | 271,893 |

Best 12-slot layout by exhaustive search over the layouts (rate-only, no storage limit, upstream machines served first): **12 Miners, 0 Smelters, 0 Presses, 0 Assemblers**, selling Ore at the maximum sale multiplier x1.8073. One Miner yields $4,914,034 per second. An Assembler alone would make $7,848,686 per second of Gears, but a balanced Gear chain needs 3.686 slots per Assembler (1 Assembler, 1.1429 Presses, 1.1429 Smelters, 0.4000 Miners), which is $2,129,489 per second per slot.

- **Rate-only ceiling**: $58,968,411 per second (12 Miners, level 100, all bonuses; revision 1: $37,568,021). The chain ceiling per slot ($2,129,489) stays below the Miner ceiling per slot ($4,914,034): at the ceiling Ore is still the better slot, as in r1. The ceiling is not reachable (see the next point), so it does not decide the first two hours; the trace probe `probe.ore-only` measures the reachable part (section 5 of `balance-trace-r2.md`).
- **Storage-capped ceiling**: a cycle never lasts less than 0.1 s (`minCycleTicks`, at most 10 completions per second), and a completion moves at most the global capacity of 16,000 units. With Ore at x1.8073 that is at most 10 x 16,000 x $1 x 1.8073 = $289,175 per second (all Ore); the same bound for a stock of pure Gears is $9,237,381 per second and is not reachable, because Gears need Plates, Bars and Ore in stock first.
- **Why it is not a pacing risk by itself**: raising one Miner from level 1 to 100 costs $267,539,272, against a prestige threshold of $10,000,000. The cost curve (x1.15 per level) outgrows the speed curve (x1.08 per level), so late levels are bought only at the end of a run.

## 3. Maximum batch per machine and partial completion against storage levels

`k = max(1, ceil(speedPpm x minCycleTicks / (durationTicks x 10^6)))` recipe units per completion (OD-6). A completion exchanges `k' = min(k, k_in, k_cap)` units (OD-9): `k_in` is the largest unit count the inputs in stock allow, `k_cap` is `floor((capacity - total stock) / net)` for a positive net change per unit `net = outputs x outputFactor - inputs`, and unbounded for `net <= 0`. The remainder of a batch is dropped, never carried over.

| Machine | Worst-case speed (ppm) | Multiple | Worst-case batch k | Cycle (ticks) | Inputs per full batch | Outputs per full batch | Net units per recipe unit |
|---|---|---|---|---|---|---|---|
| Miner | 679732586428 | x679,732.6 | 33987 | 1000011 | 0 | 271,896 | 8 |
| Smelter | 951625620998 | x951,625.6 | 47582 | 1000016 | 95,164 | 47,582 | -1 |
| Press | 951625620998 | x951,625.6 | 23791 | 1000016 | 47,582 | 23,791 | -1 |
| Assembler | 679732586428 | x679,732.6 | 13595 | 1000026 | 27,190 | 13,595 | -1 |

The worst-case speed grows against revision 1 (x503,072.8 and x704,301.9) because Calibration is now x1.8158 at its cap instead of x1.3439. The Smelter and Press worst case is 4.8 % below `maxCombinedSpeedPpm` (10^12): a further increase of Calibration or Research speed values by more than 5 % would make the level-100 speed dead (`speed.level.dead`), which the validator rejects. Without Items, Research, Industry or Overdrive, at level 100 (table speed x122,208) and at the first level that batches, the numbers of revision 1 hold unchanged, because the machine curves, recipes, milestones and storage table are unchanged:

| Machine | First level with k > 1 | Batch k at level 100 (no boosts) | Inputs per full batch | Outputs per full batch | First storage level whose capacity holds one full batch (inputs and outputs) |
|---|---|---|---|---|---|
| Miner | 25 | 6111 | 0 | 12,222 | level 5 (16,000) |
| Smelter | 25 | 6111 | 12,222 | 6,111 | level 5 (16,000) |
| Press | 31 | 3056 | 6,112 | 3,056 | level 4 (8,000) |
| Assembler | 34 | 2445 | 4,890 | 2,445 | level 4 (8,000) |

Where a batch exceeds a storage level it **completes partially** (valid, OD-9): the Miner (net +8 Ore per recipe unit with the output factor 4) is capacity-limited in the worst case at every storage level, because a worst-case batch moves 271,896 Ore against 16,000 at the top level; Smelter, Press and Assembler (net -1) are only input-limited.

### Income lost to partial batches (OD-6, OD-9)

The reference policy of `balance-policy.md` fills the store, so the trace measures partial completions and machines that stand still on a full store. A completion that the inputs limit loses nothing (the machine moves what exists); only the store-limited completions (always Miners) lose output. The numbers are those of run 1 up to the first prestige (6,940 s, 3,562 sampled completions, sampled at every look as in revision 1; `partial_*` and `store_blocked_samples` in `trace-r2.csv`):

| Measure (run 1, real catalog, to the prestige) | Value |
|---|---|
| Machine-look samples | 3,563 |
| Samples where the last completion was partial | 1,454 (40.8 % of the 3,562 sampled completions), mean fill of a partial batch 23.6 % |
| of which limited by the store (Miners only) | 706 |
| of which limited by the inputs (Smelter 672, Assembler 52, Press 24) | 748 |
| Machine-look samples with a machine stopped on a full store | 453 (12.7 %) |
| Recipe units that store-limited partial completions could not move (sampled) | 27,370 |
| Income lost to store-limited partial batches, **lower bound** (the sampled completion only, base prices, no output factor, no sale modifier) | $54,740 (0.55 % of the $10,019,139 Factory Value at the prestige) |
| Income lost, **epoch bound** (assumes every completion of the 20 s until the next look was as short as the sampled one; an upper bound, not a measurement) | $10,846,191 |

Reading: the *measured* loss is small (about half a percent of the run's value) because a Miner that fills the store stops, and the player's next look sells the stock. The epoch bound shows how large the loss could be if machines did not stop, and is quoted only as a ceiling. The larger effect is the stopped machine: one machine-look in eight found a machine idle on a full store (revision 1 did not measure it). Revision 2 reaches storage level 4 by minute 60 and level 5 by minute 90 (storage upgrades: 4 in run 1; revision 1 bought one in six hours), which is where most of that improvement comes from: the Miner batch outgrows storage levels 1 and 2 long before level 3 was affordable in revision 1. The owner decisions in `balance-trace-r2.md` section 6 do not need a change to OD-6 or OD-9.

## 4. Maximum combined multiplier per effect kind (ECO-6)

A source is **dead** when removing it does not change the maximum, because a cap already binds. No source below is dead.

| Effect kind | Sources (at maximum) | Maximum combined | Cap / bound | Dead source |
|---|---|---|---|---|
| `machineSpeed` (Miner / Smelter / Press / Assembler) | level-100 table x122,208.3, Research x1.05^5 (own machine), Industry x1.22^3 = x1.8158, best Item per key (factory x1.2; type x1.4 on Smelter and Press), Overdrive x2 | x679,732.6, x951,625.6, x951,625.6, x679,732.6 | `maxCombinedSpeedPpm` 10^12 (x1,000,000): worst case x951,625.6 | none (every source is above x1) |
| `machineOutput` (Miner) | Item `diamond-drill-head` x2, Research `deep-veins` x2 (level 1) | x4 | `maxCombinedOutputFactor` 4: the product reaches the bound exactly and never clamps | none (without either source the product is 2, below the bound) |
| `salePrice` | Research `bargaining` 1.05^5, Research `market-analysis` (Gear only) 1.10^3, Industry `market-contacts` 1.19^2 | Ore, Bar, Plate x1.8073; Gear x2.4056 | bounded below, see section 5 | none |
| `upgradeCost` | Research `tool-discount` 0.95^3 and Industry `toolmaker` 0.639 (machines); Research `warehouse-planning` 0.90^3 (storage and factory expansion) | machines x0.5479; expansion x0.7290 | each factor in [0.1, 1); costs fold with `ceil`, so a positive cost never reaches 0 | none |
| `startingCash` | Industry `seed-capital` +$300 x 10 levels | +$3,000 | 0.03 % of the threshold | none |
| `startingStorageLevel` | Industry `warehouse` +1 x 2 levels | +2 levels (start at storage level 3, 4,000) | below the 5-level table | none |

ECO-3 (capped Industry): every Industry `maxLevel` is at most 10; the combined Industry speed is x1.8158 (the canonical cap is x2), the combined Industry sale price x1.4161 and the Industry cost factor x0.639, so the run-time compounding that run 3 and later see is bounded by x1.8158 on speed. The trace shows that the speed and price bounds are *used up* by the first prestige; only the cost factor, starting Cash and starting storage are left for later runs (`balance-trace-r2.md` section 5).

**Item accrual (ECO-6)**: unchanged from revision 1: a run grants at most 4 + 3 + 3 = 10 Items; the real runs 1 to 5 of revision 2 grant Tier I x4, II x3, III x3 (the bound).

## 5. Sale-price effect on Factory Value (ECO-7)

Stock enters the Factory Value at market price with the active sale-price modifiers (FR-020). The bound: the largest stock the top storage level holds (16,000 units) at the largest sale multiplier of each resource.

| Resource | Base price | Maximum sale multiplier (ppm) | Maximum stock value at 16,000 units |
|---|---|---|---|
| `iron.ore` | $1 | 1807341 | $28,917 |
| `iron.bar` | $3 | 1807341 | $86,752 |
| `iron.plate` | $8 | 1807341 | $231,339 |
| `iron.gear` | $24 | 2405568 | $923,738 |

The worst case is a store full of Gears: **$923,738** (923738 dollars), which is 9.24 % of the $10,000,000 threshold (revision 1: 7.95 %). The test asserts at most 10 %, so revision 2 leaves 0.76 percentage points: no further raise of Market Contacts, Bargaining or Gear Market Analysis is possible without lowering another source. `removalRefundPpm` is 500,000 (below 10^6), so a build followed by a removal strictly lowers the Factory Value.

## 6. Cash-to-market ratio per offer (ECO-5)

Market value of an offer is `quantity x basePrice`; the validator requires `1 <= reward / market <= maxRewardRatioPpm / 10^6` (2.0) at every completion index 0 to 1,023.

| Offer | Kind | Quantity (index 0 / 1,023) | Cash reward (index 0 / 1,023) | Designed ratio | Ratio at index 0 | Minimum over 0 to 1,023 | Maximum over 0 to 1,023 |
|---|---|---|---|---|---|---|---|
| `contract.gear` | gear | 100 / 4,000,095 | $4,000 / $106,648,884 | 1.67 | 1.667 | 1.1109 | 1.6667 |
| `contract.plate` | plate | 100 / 4,000,095 | $1,400 / $38,193,938 | 1.75 | 1.750 | 1.1935 | 1.7500 |
| `contract.timed-bar` | timedBar | 100 / 4,000,095 | $510 / $14,734,084 | 1.70 | 1.700 | 1.2278 | 1.7030 |

The Plate and timed-Bar ratios leave less than 0.30 and 0.25 of headroom to the 2.0 bound, so the only direction in which telemetry could still raise those two rewards is a rise of at most about 14 % (Plate) and 17 % (timed Bar); the step table in `balance-trace-r2.md` confirms that +20 % is rejected for both (`contract.reward.ratio`); the `balance-trace-r2.md` section 7 lists this next to each value. RP rewards are flat (growth 10^6), so they never outgrow the quantity curve.

## 7. Unlocks, reachability and Gear timing

Unchanged from revision 1 (initial unlocks, the unlock chain, `unlock.unreachable`, `id.reserved`). The measured timing replaces the fluid model of revision 1: the first Gear is produced at 720 s (12 minutes; revision 1: 1,240 s), factory level 3 is owned before the first Gear (factory level 2 at the 10-minute row, level 3 at the 15-minute row), the first prestige is reached at 6,940 s (115.7 minutes), and the second run reaches it at 4,640 s (77.3 minutes); the trace and its caveats are in `balance-trace-r2.md`.

## 8. Assumptions and choices the spec did not fix

- Every value of section 0 is a hypothesis fitted to the *reference policy* of `balance-policy.md`, which has two artifacts that this task found and did not change (`balance-trace-r2.md` section 8): the observed income it uses to decide when to save is near zero, and its Industry order buys Seed Capital before the speed and price upgrades. A different, better policy would find a different optimum.
- Start Cash must be at least the Miner base cost (a soft lock otherwise); this is tested (`StartingCashBuysAtLeastTheFirstMiner...`) and not validated by the content validator, which would need a production change (not in T125).
- The Industry front-loading (strong first levels, low IP costs) is a design choice made to place the second run in the SC-004 band under the canonical Industry caps; the owner may prefer a different shape.

## 9. Evidence map

- `Assets/IdleFactory/Tests/EditMode/RunContentCanonicalTests.cs`: load through the dispatcher as revision 2, canonical rendering, literal FR numbers, references, T090 keys, worst-case speed and batch (sections 3 and 4 of this record), output factor, sale-price bound (section 5), cash ratio range (section 6), this record.
- `Assets/IdleFactory/Tests/EditMode/Balance/BalanceTraceTests.cs`: the golden trace `trace-r2.csv`, the ECO-2 bands and ECO-3 run times asserted for revision 2, the recorded sensitivity deviations, the start-Cash invariant, this record's partial-batch figures, and `trace-r1.csv` kept as history.
- `specs/003-first-factory-prestige/contracts/gameplay-content-v2.md` and `docs/content/json-content-pipeline.md`: unchanged.
