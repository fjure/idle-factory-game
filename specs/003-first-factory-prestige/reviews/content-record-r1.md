# Content record r1: initial Iron factory content sheet

**Task**: T097 (GitHub Issue #179), Game Systems/Economy author. Review lenses: Game Designer/Product Owner (values), Software Architect (validator additions), Test/QA.
**Document**: `Assets/IdleFactory/Content/Iron/iron-factory-content.json`, `contentSchemaVersion` 2, `contentRevision 1`.
**Status**: a **balance hypothesis**. Only the FR-002, FR-007, FR-012 and FR-021 targets (and the FR-008, FR-010, FR-018, FR-019 numbers the spec names) are authoritative. Every other value was chosen by this sheet, is unapproved, and is calibrated by the balance trace (T107) and the owner approval (T127, R13). Nothing here is a promise about pacing.
**Evidence**: every number below is recomputed from the shipped document by `RunContentCanonicalTests` (worst-case speed, batch, sale-price stock bound and the cash ratio range are asserted against this record), and the document passes `RunContentValidator`, including `speed.level.dead`, `batch.overflow`, `id.reserved` and `unlock.unreachable`.

## 1. Values fixed by the spec versus values chosen by this sheet

| Area | Value | Source |
|---|---|---|
| Recipes | Miner 2 Ore / 2 s (no input); Smelter 2 Ore -> 1 Bar / 2 s; Press 2 Bars -> 1 Plate / 4 s; Assembler 2 Plates -> 1 Gear / 5 s | FR-002 (spec). One Press supplies 5/8 = 62.5 % of one Assembler's Plate demand (derived by the test from the recipes) |
| Market | Ore $1, Bar $3, Plate $8, Gear $24 | FR-012 (spec) |
| Storage | 1,000 / 2,000 / 4,000 / 8,000 / 16,000 at $0 / 2,500 / 10,000 / 40,000 / 160,000 | FR-007 (spec) |
| Factory slots | 4 / 6 / 8 / 10 / 12 at levels 1 to 5 | FR-008 (spec) |
| Factory costs | $0 / 800 / 3,000 / 12,000 / 48,000 | **chosen** (the spec leaves them to the sheet) |
| Machine curves | cost growth 1,150,000 ppm (ceil), speed growth 1,080,000 ppm, milestones x2 / x2 / x3 / x5 at levels 10 / 25 / 50 / 100, `maxLevel` 100 | FR-010 (spec: "near"); `maxLevel` 100 is **chosen** (it is the smallest level at which the level-100 milestone exists) |
| Machine base costs | Miner $10, Smelter $40, Press $150, Assembler $600 | **chosen** |
| Start | `startingCash` $25 (two Miners), `initialUnlocks` Iron Ore and the Miner, `removalRefundPpm` 500,000 | refund per spec task text; cash **chosen** |
| Run bounds | `minCycleTicks` 1,000,000 (0.1 s), `maxCombinedSpeedPpm` 10^12 (the contract maximum), `maxCombinedOutputFactor` 4, `maxRewardRatioPpm` 2,000,000 | **chosen** (OD-6, GD-5) |
| Overdrive | x2 for 30 s, five-minute cooldown, unlocked at factory level 2 | FR-019 (spec); unlock **chosen** |
| Prestige | threshold $10,000,000, IP = floor(10 x sqrt(FV / 10^7)) | FR-021 (spec): 10 IP at $10M, 20 at $40M, 30 at $90M, 100 at $1B (asserted) |
| Offline policy | revision 1, 4/5, 8 h | locked (contract) |

### Contracts, Items, Research, Industry (all chosen unless noted)

| Contract | Kind | Resource | Quantity (completion 0, growth) | Cash reward (completion 0) | Duration | Item grants (completion indices) | RP per completion |
|---|---|---|---|---|---|---|---|
| `contract.gear` | gear | `iron.gear` | 100, x1.01 (ceil) | $3,840, x1.01 (ceil) | none | `item.overclock-core` at [1, 5, 10] | 8 flat |
| `contract.plate` | plate | `iron.plate` | 100, x1.01 (ceil) | $1,280, x1.01 (ceil) | none | `item.flux-catalyst` at [1, 4, 9] | 4 flat |
| `contract.timed-bar` | timedBar | `iron.bar` | 100, x1.01 (ceil) | $510, x1.01 (ceil) | 240 s | `item.diamond-drill-head` at [0, 3, 6, 10] | 2 flat |

| Item | Tier | Target | Effect | Duration |
|---|---|---|---|---|
| `item.diamond-drill-head` (Diamond Drill Head) | 1 | machine type machine.miner | machineOutput x2 | 60 s |
| `item.flux-catalyst` (Flux Catalyst) | 2 | machine type machine.press, machine.smelter | machineSpeed x1.40 | 90 s |
| `item.overclock-core` (Overclock Core) | 3 | factory | machineSpeed x1.20 | 120 s |

The Tier I Diamond Drill Head is the spec's literal (x2 Miner output for 60 s). Tier II and III names are the Art Director proposals of the T090 brief; their effects and durations are chosen (1 to 2 minutes, FR-018). All three tiers are granted within the first completions of a first run: Tier I at the first timed-Bar completion (index 0), Tier II at the second Plate completion (index 1), Tier III at the second Gear completion (index 1).

| Research | Effect (per level) | maxLevel | RP cost (level 1, growth) | Prerequisites |
|---|---|---|---|---|
| `research.assembler-tuning` | machineSpeed machine.assembler x1.05 | 5 | 12, x1.50 | research.press-tuning L2 |
| `research.bargaining` | salePrice allResources x1.05 | 5 | 4, x1.50 | none |
| `research.deep-veins` | machineOutput machine.miner x2 | 1 | 40, x1.50 | research.miner-tuning L3 |
| `research.market-analysis` | salePrice iron.gear x1.10 | 3 | 20, x1.60 | research.bargaining L2 |
| `research.miner-tuning` | machineSpeed machine.miner x1.05 | 5 | 3, x1.50 | none |
| `research.press-tuning` | machineSpeed machine.press x1.05 | 5 | 8, x1.50 | research.smelter-tuning L2 |
| `research.smelter-tuning` | machineSpeed machine.smelter x1.05 | 5 | 5, x1.50 | research.miner-tuning L2 |
| `research.tool-discount` | upgradeCost allMachines x0.95 | 3 | 10, x1.50 | research.miner-tuning L1 |
| `research.warehouse-planning` | upgradeCost expansion x0.90 | 3 | 25, x1.50 | research.tool-discount L2 |

The graph is acyclic with two roots (`research.bargaining`, `research.miner-tuning`) and chains up to depth 3 (`miner-tuning` -> `smelter-tuning` -> `press-tuning` -> `assembler-tuning`).

| Industry upgrade | Effect (per level) | maxLevel | IP cost (level 1, growth) |
|---|---|---|---|
| `industry.calibration` | machineSpeed allMachines x1.03 | 10 | 8, x1.50 |
| `industry.market-contacts` | salePrice allResources x1.02 | 10 | 10, x1.50 |
| `industry.seed-capital` | startingCash  +500 | 10 | 5, x1.50 |
| `industry.toolmaker` | upgradeCost allMachines x0.98 | 5 | 12, x1.60 |
| `industry.warehouse` | startingStorageLevel  +1 | 2 | 20, x2.00 |

The first IP award at the threshold is 10, which affords the level-1 cost of `industry.calibration` (8), `industry.seed-capital` (5) and `industry.market-contacts` (10). The spec families map as follows: starting Cash (`seed-capital`), upgrade costs (`toolmaker`), starting storage (`warehouse`), speed (`calibration`) and production value (`market-contacts`, a sale-price multiplier). The contract gives Industry no `machineOutput` source, so a pure output-factor family does not exist (`effect.kind.unknown` for Industry), and "production" is realized as sale value.

## 2. Theoretical maximum income per second

Assumptions: every machine at level 100, every speed source at its maximum (Research, Industry, the best Item per conflict key, Overdrive active), every sale-price source at its maximum, output factor 4 on the Miner. This is a ceiling, not an expectation: a machine reaching level 100 costs more than the whole run is worth (see the cost row below).

| Machine | Executions per second per machine (worst-case speed / recipe duration) | Units produced per second | Units consumed per second |
|---|---|---|---|
| Miner | 251,536.41 | 2,012,291 | 0 |
| Smelter | 352,150.97 | 352,151 | 704,302 |
| Press | 176,075.48 | 176,075 | 352,151 |
| Assembler | 100,614.56 | 100,615 | 201,229 |

Best 12-slot layout by exhaustive search over layouts and an exact linear program over the utilizations (rate-only, no storage limit): **12 Miners, 0 Smelters, 0 Presses, 0 Assemblers**, selling Ore at the maximum sale multiplier x1.5558. The Miner's x4 output factor makes raw Ore the best use of a slot: one Miner yields 3,130,668 dollars per second. An Assembler alone would make 5,000,309 dollars per second of Gears, but a balanced Gear chain needs 3.69 slots per Assembler (1 Assembler, 1.14 Presses, 1.14 Smelters, 0.40 Miners), which is 1,356,673 dollars per second per slot.

- **Rate-only ceiling**: $37,568,021 per second (12 Miners, level 100, all bonuses).
- **Storage-capped ceiling**: a cycle never lasts less than 0.1 s (`minCycleTicks` 1,000,000, so at most 10 completions per second), and a completion moves at most the global capacity of 16,000 units. With Ore at x1.5558 that is at most 10 x 16,000 x $1 x 1.5558 = $248,923 per second (all Ore); the same bound for a stock of pure Gears is $7,951,620 per second and is not reachable, because Gears need Plates, Bars and Ore in stock first.
- **Why it is not a pacing risk by itself**: raising one Miner from level 1 to 100 costs $102,277,099 in total (Miner), against a prestige threshold of $10,000,000. The cost curve (x1.15 per level) outgrows the speed curve (x1.08 per level), so late levels are bought only at the end of a run.

## 3. Maximum batch per machine and partial completion against storage levels

`k = max(1, ceil(speedPpm x minCycleTicks / (durationTicks x 10^6)))` recipe units per completion (OD-6). A completion exchanges `k' = min(k, k_in, k_cap)` units (OD-9): `k_in` is the largest unit count the inputs in stock allow, `k_cap` is `floor((capacity - total stock) / net)` for a positive net change per unit `net = outputs x outputFactor - inputs`, and unbounded for `net <= 0`. The remainder of a batch is dropped, never carried over.

| Machine | Worst-case speed (ppm) | Multiple | Worst-case batch k | Cycle (ticks) | Inputs per full batch | Outputs per full batch | Net units per recipe unit |
|---|---|---|---|---|---|---|---|
| Miner | 503072810308 | x503,072.8 | 25154 | 1000015 | 0 | 201,232 | 8 |
| Smelter | 704301934430 | x704,301.9 | 35216 | 1000026 | 70,432 | 35,216 | -1 |
| Press | 704301934430 | x704,301.9 | 17608 | 1000026 | 35,216 | 17,608 | -1 |
| Assembler | 503072810308 | x503,072.8 | 10062 | 1000055 | 20,124 | 10,062 | -1 |

Without Items, Research, Industry or Overdrive, at level 100 (table speed x122,208) and at the first level that batches:

| Machine | First level with k > 1 | Batch k at level 100 (no boosts) | Inputs per full batch | Outputs per full batch | First storage level whose capacity holds one full batch (inputs and outputs) |
|---|---|---|---|---|---|
| Miner | 25 | 6111 | 0 | 12,222 | level 5 (16,000) |
| Smelter | 25 | 6111 | 12,222 | 6,111 | level 5 (16,000) |
| Press | 31 | 3056 | 6,112 | 3,056 | level 4 (8,000) |
| Assembler | 34 | 2445 | 4,890 | 2,445 | level 4 (8,000) |

Where a batch exceeds a storage level it **completes partially** (valid, OD-9):

- **Miner** (net +2 Ore per unit, +8 with the output factor 4): a worst-case batch moves 201,232 Ore, above the 16,000 top capacity, so the `k_cap` rule makes every completion partial in the worst case at every storage level. With no boosts at level 100 it moves 12,222 Ore, which fits only storage level 5.
- **Smelter, Press, Assembler** (net -1): never capacity-limited (`k_cap` is unbounded), only input-limited. A worst-case Smelter batch consumes 70,432 Ore, more than any storage level holds, so it completes the units the stock allows; Press and Assembler batches (35,216 Bars, 20,124 Plates) behave the same way.
- Partial completion therefore only occurs once a machine is deep into its level range and boosted; the balance trace (T107) records how often the reference policy completes partial batches.

## 4. Maximum combined multiplier per effect kind (ECO-6)

A source is **dead** when removing it does not change the maximum, because a cap already binds. No source below is dead.

| Effect kind | Sources (at maximum) | Maximum combined | Cap / bound | Dead source |
|---|---|---|---|---|
| `machineSpeed` (Miner / Smelter / Press / Assembler) | level-100 table x122,208.3, Research x1.05^5 (own machine), Industry x1.03^10 = x1.3439, best Item per key (factory x1.2; type x1.4 on Smelter and Press), Overdrive x2 | x503,072.8, x704,301.9, x704,301.9, x503,072.8 | `maxCombinedSpeedPpm` 10^12 (x1,000,000): worst case x704,301.9 | none (every source is above x1) |
| `machineOutput` (Miner) | Item `diamond-drill-head` x2, Research `deep-veins` x2 (level 1) | x4 | `maxCombinedOutputFactor` 4: the product reaches the bound exactly and never clamps | none (without either source the product is 2, below the bound) |
| `salePrice` | Research `bargaining` 1.05^5, Research `market-analysis` (Gear only) 1.10^3, Industry `market-contacts` 1.02^10 | Ore, Bar, Plate x1.5558; Gear x2.0707 | bounded below, see section 5 | none |
| `upgradeCost` | Research `tool-discount` 0.95^3 and Industry `toolmaker` 0.98^5 (machines); Research `warehouse-planning` 0.90^3 (storage and factory expansion) | machines x0.7750; expansion x0.7290 | each factor in [0.1, 1); costs fold with `ceil`, so a positive cost never reaches 0 | none |
| `startingCash` | Industry `seed-capital` +$500 x 10 levels | +$5,000 | 0.05 % of the threshold | none |
| `startingStorageLevel` | Industry `warehouse` +1 x 2 levels | +2 levels (start at storage level 3, 4,000) | below the 5-level table | none |

ECO-3 (capped Industry): every Industry `maxLevel` is at most 10; the combined Industry speed is x1.344 and the combined sale price x1.219, so the run-time compounding that run 3 and later would see is bounded by x1.344 on speed. The balance trace (T107) must still project runs 3 to 5.

**Item accrual (ECO-6)**: the Item grants are indexed by completion count (the contract counters reset on prestige). A run can grant at most 4 + 3 + 3 = 10 Items (`timed-bar` at 0, 3, 6, 10; `plate` at 1, 4, 9; `gear` at 1, 5, 10), at most 4 of one tier. Unused Items survive prestige and the stack is not capped beyond the 10^18 exact bound (R12); the trace records accrual across runs. A single activation lasts 60 to 120 s and conflicts by `(effect kind, target)`, so stacked Items of one key do not add.

## 5. Sale-price effect on Factory Value (ECO-7)

Stock enters the Factory Value at market price with the active sale-price modifiers (FR-020). The bound: the largest stock the top storage level holds (16,000 units) at the largest sale multiplier of each resource.

| Resource | Base price | Maximum sale multiplier (ppm) | Maximum stock value at 16,000 units |
|---|---|---|---|
| `iron.ore` | $1 | 1555773 | $24,892 |
| `iron.bar` | $3 | 1555773 | $74,677 |
| `iron.plate` | $8 | 1555773 | $199,138 |
| `iron.gear` | $24 | 2070736 | $795,162 |

The worst case is a store full of Gears: **$795,162** (795162 dollars), which is 7.95 % of the $10,000,000 threshold. The test asserts at most 10 %. `removalRefundPpm` is 500,000 (below 10^6), so a build followed by a removal strictly lowers the Factory Value; the validator range 0 to 999,999 enforces that for every sheet.

## 6. Cash-to-market ratio per offer (ECO-5)

Market value of an offer is `quantity x basePrice`; the validator requires `1 <= reward / market <= maxRewardRatioPpm / 10^6` (2.0) at every completion index 0 to 1,023. The quantity and Cash curves both use `ceil` with the same growth (x1.01), so rounding can only pull the ratio down while the quantity is small: a quantity of 100 grows 1, 1, 1, ... per step at first, which makes the quantity run ahead of its exact curve. The designed ratios therefore sit well above 1.0 and the minimum over 1,024 completions is what the test asserts.

| Offer | Kind | Quantity (index 0 / 1,023) | Cash reward (index 0 / 1,023) | Designed ratio | Ratio at index 0 | Minimum over 0 to 1,023 | Maximum over 0 to 1,023 |
|---|---|---|---|---|---|---|---|
| `contract.gear` | gear | 100 / 4,000,095 | $3,840 / $102,473,008 | 1.60 | 1.600 | 1.0674 | 1.6002 |
| `contract.plate` | plate | 100 / 4,000,095 | $1,280 / $35,017,369 | 1.60 | 1.600 | 1.0943 | 1.6002 |
| `contract.timed-bar` | timedBar | 100 / 4,000,095 | $510 / $14,734,084 | 1.70 | 1.700 | 1.2278 | 1.7030 |

RP rewards are flat (growth 10^6), so they never outgrow the quantity curve (`contract.rp.growth`). RP per run is bounded by the completions actually made, and farming small contracts is limited because the quantity per completion grows by at least 1 each time.

## 7. Unlocks, reachability and Gear timing

Initial unlocks: Iron Ore and the Miner. The chain, followed by the static fixpoint `unlock.unreachable`: Bar and Smelter unlock when Ore is produced (the Miner produces it); Plate and the Press at factory level 2; Gear, the Assembler at factory level 3; contracts when their resource is produced; Overdrive at factory level 2. Every resource, machine, contract offer and Overdrive is reachable; every Research is purchasable once RP is paid by an unlocked contract (the timed Bar contract pays RP). `id.reserved` rejects the ID `overdrive`; none of the sheet's IDs uses it.

### Indicative timing (fluid model, not the T107 trace)

A deterministic fluid model (continuous flows, 30-second action cadence, build one of every unlocked machine type, buy an expansion when slots are full, otherwise the best payback, storage bought when throughput nears the 10 completions per second bound; no contracts, Items, Research or Overdrive) gives, for the shipped values:

| Event | Time (minutes) |
|---|---|
| first Smelter | 0.5 |
| factory level 2 | 4.5 |
| first Press | 5.0 |
| factory level 3 (Gear and Assembler unlock) | 11.5 |
| first Assembler | 13.5 |
| first Gear produced | 13.5 |
| Factory Value $100,000 | 29.7 |
| Factory Value $1,000,000 | 46.3 |
| Factory Value $10,000,000 | 61.4 |

The model plays perfectly and ignores friction, so real times are later. Gear appears at about 13.5 minutes in the model, which puts Gear near the SC-003 window of 10 to 15 minutes once friction is added. The **first prestige is a risk**: the model reaches $10,000,000 at about 61 minutes against the 120-minute target. Content levers the T127 calibration can use without a code change: the Miner base cost and cost growth, the Industry per-level values, the research per-level values, factory and storage costs. The spec-fixed numbers (FR-002, FR-007, FR-010 growth and milestones, FR-012) stay as they are. The model also ignores the global storage ceiling on late throughput beyond the rule above, which T107 measures with the real simulator.

## 8. Assumptions and choices the spec did not fix

- `maxLevel` 100 for every machine, so that the level-100 milestone (x5) exists; a lower `maxLevel` would make the FR-010 milestone list invalid.
- Resource IDs `iron.ore`, `iron.bar`, `iron.plate`, `iron.gear` (the Gear sprite key is `F003-RES-GEAR`); machine IDs `machine.miner`, `machine.smelter`, `machine.press`, `machine.assembler`; Item IDs follow the T090 key names.
- Contract quantities start at 100 units with growth x1.01; reward ratios 1.6 (Plate, Gear) and 1.7 (timed Bar, which has a 4-minute deadline), chosen so that the ceil rounding keeps every ratio between 1.0 and 2.0 over 1,024 completions.
- Tier II (Flux Catalyst, x1.4 speed on Smelter and Press, 90 s) and Tier III (Overclock Core, x1.2 factory-wide speed, 120 s) effects and durations; the research and Industry values in section 1.
- Overdrive unlocks at factory level 2; the Industry upgrade set is the five rows of section 1.
- Start cash $25 and the factory costs were set with the fluid model of section 7 so that the Press and the Assembler are reachable in the first quarter hour; they are hypotheses.

## 9. Evidence map

- `Assets/IdleFactory/Tests/EditMode/RunContentCanonicalTests.cs`: load through the dispatcher, canonical rendering, literal FR numbers (62.5 % derived), references, T090 keys, worst-case speed and batch, output factor, sale-price bound, cash ratio range, this record.
- `Assets/IdleFactory/Tests/EditMode/RunContentValidatorTests.cs`: `id.reserved`, `unlock.unreachable` and their fixtures.
- `specs/003-first-factory-prestige/contracts/gameplay-content-v2.md` and `docs/content/json-content-pipeline.md`: the two new rule codes.
