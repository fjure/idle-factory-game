# Contract: Gameplay Content Document v2

**Consumers**: `RunContentReader` → `RunContentValidator` → `RunCatalogBuilder` (Unity assembly), content authors, the balance trace, and review gates.
**Normative for**: FR-001, FR-002, FR-007–FR-010, FR-012–FR-023, FR-029, FR-030.
The author copy of this contract is a new version-2 section in `docs/content/json-content-pipeline.md`. The two must agree, and this file wins until the author doc is accepted.

## Carried over from v1 unchanged

The v1 rules below apply to v2 exactly as written in [Feature 002 contract](../../002-json-content-authoring/contracts/gameplay-content-v1.md):

- **Envelope**: strict UTF-8 without a BOM, at most 1 MiB, depth ≤ 16, integers only (no fraction or exponent), no `null`, no comments. Unknown, duplicate and missing members are rejected.
- **Limits**: 1,024 entries per top-level family, 256 per nested list, and IDs of at most 128 UTF-16 code units that are unchanged by trim and NFC.
- **Diagnostics format**: `code`, JSON Pointer path, `entryId` and message. Structural failures report the first violation with its line and column; semantic failures report every violation, up to 100, in document order.
- **All-or-nothing loading**: the document either loads completely or is rejected.
- **Canonical form**: a review gate, not a runtime rule. Every array of objects is sorted by `id`, and the `level` tables by `level`.

`contentSchemaVersion` is peeked first. Version `1` routes to the unchanged Feature 002 loader, which serves only as the legacy import catalog. Version `2` routes to the reader defined here. Any other value is rejected with `content.version.unsupported`.

## Document shape (v2, abbreviated and illustrative)

```json
{
  "contentSchemaVersion": 2,
  "contentRevision": 1,
  "runStart": { "startingCash": 25, "initialUnlocks": ["iron.ore", "machine.miner"], "removalRefundPpm": 500000,
                "minCycleTicks": 1000000, "maxCombinedSpeedPpm": 100000000, "maxCombinedOutputFactor": 4,
                "maxRewardRatioPpm": 2000000 },
  "resources": [ { "id": "iron.ore", "label": "Iron Ore", "presentationKey": "F003-RES-IRON-ORE", "basePrice": 1,
                   "unlock": { "kind": "always" } } ],
  "recipes": [ { "id": "recipe.mine-iron-ore", "inputs": [], "outputs": [ { "resourceId": "iron.ore", "amount": 2 } ],
                 "durationTicks": 20000000 } ],
  "machines": [ { "id": "machine.miner", "label": "Miner", "presentationKey": "F003-MACHINE-MINER",
                  "recipeId": "recipe.mine-iron-ore", "maxLevel": 25,
                  "cost": { "base": 10, "growthPpm": 1150000, "rounding": "ceil" },
                  "speedGrowthPpm": 1080000,
                  "milestones": [ { "level": 10, "speedPpm": 2000000 }, { "level": 25, "speedPpm": 2000000 } ],
                  "unlock": { "kind": "always" } } ],
  "storageLevels": [ { "level": 1, "capacity": 1000, "cost": 0 }, { "level": 2, "capacity": 2000, "cost": 2500 } ],
  "factoryLevels": [ { "level": 1, "slots": 4, "cost": 0 }, { "level": 2, "slots": 6, "cost": 5000 } ],
  "contracts": [ { "id": "contract.timed-bar", "kind": "timedBar", "resourceId": "iron.bar",
                   "quantity": { "base": 40, "growthPpm": 1010000, "rounding": "ceil" }, "durationMs": 180000,
                   "rewards": [ { "kind": "cash", "curve": { "base": 200, "growthPpm": 1010000, "rounding": "ceil" } },
                                { "kind": "item", "itemId": "item.diamond-drill-head", "onCompletionIndices": [0] } ],
                   "unlock": { "kind": "resourceProduced", "id": "iron.bar" } } ],
  "items": [ { "id": "item.diamond-drill-head", "tier": 1, "label": "Diamond Drill Head",
               "presentationKey": "F003-ITEM-DIAMOND-DRILL-HEAD", "durationMs": 60000,
               "target": { "kind": "machineType", "allowedMachineIds": ["machine.miner"] },
               "effect": { "kind": "machineOutput", "factor": 2 } } ],
  "research": [ { "id": "research.smelter-speed", "label": "Smelter Tuning", "maxLevel": 5,
                  "rpCost": { "base": 5, "growthPpm": 1500000, "rounding": "ceil" }, "prerequisites": [],
                  "effect": { "kind": "machineSpeed", "target": { "kind": "machine", "id": "machine.smelter" },
                              "valuePerLevelPpm": 1100000 } } ],
  "overdrive": { "durationMs": 30000, "cooldownMs": 300000, "speedPpm": 2000000, "unlock": { "kind": "always" } },
  "prestige": { "thresholdFactoryValue": 10000000, "ipCurve": { "kind": "sqrt", "coefficient": 10, "divisor": 10000000 } },
  "industryUpgrades": [ { "id": "industry.starting-cash", "label": "Seed Capital", "maxLevel": 10,
                          "ipCost": { "base": 5, "growthPpm": 1500000, "rounding": "ceil" },
                          "effect": { "kind": "startingCash", "valuePerLevel": 500 } } ],
  "offlinePolicies": [ { "revision": 1, "efficiencyNumerator": 4, "efficiencyDenominator": 5, "maxCreditedTicks": 288000000000 } ]
}
```

Values shown are placeholders illustrating shape, but the complete example in the author guide satisfies every rule below (GD-5), checked by `RunContentValidatorTests`: the Miner's worst-case composed speed (level 25 ≈ ×25.4, factory-wide Item ×1.2, Overdrive ×2) is about ×61, under `maxCombinedSpeedPpm` (×100); its worst batch is 4 recipe units (32 units with the output factor 4); and every contract curve stays below 10^18 over 1,024 completion indices with a Cash-to-market ratio between 1.0 and 1.67 (the quantity and Cash curves use `ceil`, so rounding cannot push the ratio out of bounds). The FR-002, FR-007, FR-012 and FR-021 targets are authoritative. Every other number comes from the versioned initial content sheet (plan work item 5) and stays a hypothesis until balance approval (R13).

## Semantics

- **Curves** (`cost`, `quantity`, reward `curve`, `rpCost`, `ipCost`) are evaluated iteratively: `v₀ = base` and `vₙ₊₁ = round(vₙ × growthPpm / 10^6)`, where `round` is the declared `rounding`. The index is the level for costs and the completion count for contracts. The catalog precomputes a table up to `maxLevel` (costs) or 1,024 entries (contracts).
- **Machine speed**:
  - At level `L`, `SpeedTable[L]` equals the speed growth applied `L−1` times (floor) multiplied by every milestone whose level is `≤ L`, using `MulDivFloor` in ascending level order.
  - The effective speed composition order is `SpeedTable` → Research → Industry → Items (in `itemId` order) → Overdrive (R03). `maxCombinedSpeedPpm` is an overflow bound that valid content never reaches (`speed.level.dead`).
  - **Batching (OD-6)**: no cycle is shorter than `minCycleTicks`. With `D₀ = durationTicks`, the batch factor is `k = MulDivCeil(speedPpm, minCycleTicks, D₀ × 10^6)` (at least 1) and `cycleDuration = MulDivCeil(k × D₀, 10^6, speedPpm)`. A completion exchanges `k′` times the recipe's inputs and outputs (outputs also times the output factor) atomically, where `k′ ≤ k` is the largest number of units whose inputs are in stock and whose result fits the capacity; `k′ = 0` waits, and the remainder is never carried over (OD-9).
- **Unlock kinds**: `always`, `researchPurchased {id}`, `factoryLevelAtLeast {level}`, `resourceProduced {id}`. Evaluation happens at event and command timestamps. Unlocks are monotonic within a run.
- **Effect kinds and targets**:

| `kind` | Value member | Allowed `target.kind` | Sources |
|---|---|---|---|
| `machineSpeed` | `speedPpm` / `valuePerLevelPpm` (> 10^6) | `allMachines`, `machine` | Research, Industry, Item |
| `machineOutput` | `factor` (integer 2–`maxCombinedOutputFactor`) | `machineType` (Items), `machine` | Item, Research |
| `salePrice` | `valuePerLevelPpm` (> 10^6) | `allResources`, `resource` | Research, Industry |
| `upgradeCost` | `valuePerLevelPpm` (10^5 … < 10^6) | `allMachines`, `machine`, `expansion` (written `{ "kind": "expansion" }`, no `id`) | Research, Industry |
| `startingCash` | `valuePerLevel` ($) | none | Industry |
| `startingStorageLevel` | `valuePerLevel` (levels) | none | Industry |

- **Expansion target**: an `upgradeCost` effect with target `expansion` lowers the upgrade cost of both expansion tracks (storage and factory), because the domain `EffectDef` has no track discriminator. The target carries no `id`.
- **Item target** (see OD-1): `factory`, or `machineType` with a non-empty `allowedMachineIds`. `machineSpeed` and `machineOutput` Items apply to every machine of the chosen type. The conflict key is `(effect.kind, targetKey)`.
- **Build cost discount**: an `upgradeCost` effect on `allMachines` or `machine` also lowers the build (level-1) cost of the targeted machines, because the build cost is the first entry of the same cost table. The discounted cost is rounded up, so a positive cost never becomes 0, and `investedCash` stores the amount actually paid (owner decision 2026-09-30, found in T137).
- **Per-level application**: a Research or Industry effect applies its value once per purchased level, folded in ordinal ID order. This includes a Research `machineOutput` factor: at level L it multiplies the output factor L times, and the product is clamped to `maxCombinedOutputFactor` (owner decision 2026-09-30, found in T098).
- **Machine-type Item target at runtime**: when a `machineType` Item is activated, the player picks one machine ID from `allowedMachineIds`. The effect's target key is that machine ID, and the conflict key is `(effect.kind, machineId)`. The effect applies to every machine instance of that ID (owner decision 2026-09-30, found in T098).
- **Prestige IP**: `IP = ISqrt(MulDivFloor(coefficient², FV, divisor))`, which equals `floor(coefficient × sqrt(FV / divisor))`. Only `kind: "sqrt"` is supported.

## Semantic rule codes (in addition to the v1 identity and structure codes)

| Area | Codes |
|---|---|
| Identity and structure | `id.empty`, `id.too-long`, `id.not-canonical`, `id.duplicate`, `value.null`, `value.out-of-range` (a number or vocabulary value outside its range where no dedicated code exists) |
| Unlocks | `unlock.kind.unknown`, `unlock.level.invalid` (1 … number of factory levels), `ref.research.unknown` (a `researchPurchased` unlock naming no Research) |
| Run start | `runstart.unlock.unknown`, `runstart.refund.out-of-range` (0–999,999, ECO-7), `runstart.min-cycle.invalid` (< 1,000,000 ticks), `runstart.speed-bound.invalid` (outside 10^6 … 10^12), `runstart.output-bound.invalid`, `runstart.reward-ratio.invalid` (< 10^6) |
| Resources/recipes | `resource.price.negative`, `recipe.outputs.empty`, `recipe.amount.non-positive`, `recipe.duration.non-positive`, `recipe.duration.out-of-range` (> 10^12 ticks), `recipe.resource.duplicate`, `ref.resource.unknown` |
| Machines | `ref.recipe.unknown`, `machine.max-level.invalid`, `machine.milestone.level-invalid`, `machine.milestone.duplicate`, `curve.growth.invalid`, `curve.overflow`, `curve.stalled` (a growing curve that does not strictly increase at some step; flat is `growthPpm = 10^6`, ECO-8), `speed.level.dead` (`SpeedTable` not strictly increasing to `maxLevel`, or the worst-case composed speed with every speed source at its maximum above `maxCombinedSpeedPpm`; R04, OD-6), `batch.overflow` (a worst-case batch amount exceeds 10^18; R04. A batch larger than the store is valid, OD-9) |
| Expansion | `level.sequence.invalid` (must start at 1 and be contiguous), `level.first-cost.non-zero`, `storage.capacity.non-increasing`, `factory.slots.non-increasing`, `factory.slots.too-many` (> 12) |
| Contracts | `contract.kind.unknown`, `contract.kind.resource-mismatch` (each contract kind uses one resource consistently across all offers of that kind, and no resource is shared by two kinds; no fixed resource IDs), `contract.duration.required`, `contract.duration.forbidden`, `contract.reward.empty`, `contract.reward.kind.unknown`, `contract.reward.item.unknown`, `contract.kinds.incomplete` (Plate, Gear and timed Bar must each appear), `contract.reward.ratio` (a Cash reward below the market value of the required units, or above `maxRewardRatioPpm` times it, at any completion index up to 1,024; ECO-5), `contract.rp.growth` (an RP curve growing faster than its quantity curve; ECO-5) |
| Items | `item.tier.invalid`, `item.tiers.incomplete` (every tier 1–3 needs an Item obtainable from some contract reward), `item.target.invalid`, `item.duration.non-positive` |
| Research/Industry | `research.prerequisite.unknown`, `research.prerequisite.cycle`, `research.prerequisite.level-invalid`, `upgrade.max-level.invalid`, `industry.first-affordable.missing` (no level-1 cost ≤ IP at the threshold) |
| Effects | `effect.kind.unknown`, `effect.target.invalid`, `effect.value.out-of-range` |
| Overdrive/Prestige | `overdrive.value.invalid`, `prestige.threshold.invalid`, `prestige.curve.unsupported` |
| Offline policy | `policy.revision-1.required` (unchanged) |
| Presentation (Unity composition) | `presentation.key.unresolved` (unchanged, now over resources, machines and Items) |

## Evolution

- Adding IDs and retuning numbers is a new `contentRevision` inside version 2. It is save-compatible, because Save V2 rescales progress on load (R08). Removing or renaming an ID that a Save V2 references needs a save migration decision.
- A new effect kind, unlock kind, contract kind or curve kind, or a new required member, needs code and `contentSchemaVersion` 3.
