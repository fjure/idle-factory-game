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
                "maxCombinedSpeedPpm": 1000000000, "maxCombinedOutputFactor": 4 },
  "resources": [ { "id": "iron.ore", "label": "Iron Ore", "presentationKey": "F003-RES-IRON-ORE", "basePrice": 1,
                   "unlock": { "kind": "always" } } ],
  "recipes": [ { "id": "recipe.mine-iron-ore", "inputs": [], "outputs": [ { "resourceId": "iron.ore", "amount": 2 } ],
                 "durationTicks": 20000000 } ],
  "machines": [ { "id": "machine.miner", "label": "Miner", "presentationKey": "F003-MACHINE-MINER",
                  "recipeId": "recipe.mine-iron-ore", "maxLevel": 100,
                  "cost": { "base": 10, "growthPpm": 1150000, "rounding": "ceil" },
                  "speedGrowthPpm": 1080000,
                  "milestones": [ { "level": 10, "speedPpm": 2000000 }, { "level": 25, "speedPpm": 2000000 },
                                  { "level": 50, "speedPpm": 3000000 }, { "level": 100, "speedPpm": 5000000 } ],
                  "unlock": { "kind": "always" } } ],
  "storageLevels": [ { "level": 1, "capacity": 1000, "cost": 0 }, { "level": 2, "capacity": 2000, "cost": 2500 } ],
  "factoryLevels": [ { "level": 1, "slots": 4, "cost": 0 }, { "level": 2, "slots": 6, "cost": 5000 } ],
  "contracts": [ { "id": "contract.timed-bar", "kind": "timedBar", "resourceId": "iron.bar",
                   "quantity": { "base": 40, "growthPpm": 1250000, "rounding": "ceil" }, "durationMs": 180000,
                   "rewards": [ { "kind": "cash", "curve": { "base": 200, "growthPpm": 1300000, "rounding": "floor" } },
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

Values shown are placeholders illustrating shape. The FR-002, FR-007, FR-012 and FR-021 targets are authoritative. Every other number comes from the versioned initial content sheet (plan work item 5) and stays a hypothesis until balance approval (R13).

## Semantics

- **Curves** (`cost`, `quantity`, reward `curve`, `rpCost`, `ipCost`) are evaluated iteratively: `v₀ = base` and `vₙ₊₁ = round(vₙ × growthPpm / 10^6)`, where `round` is the declared `rounding`. The index is the level for costs and the completion count for contracts. The catalog precomputes a table up to `maxLevel` (costs) or 1,024 entries (contracts).
- **Machine speed**:
  - At level `L`, `SpeedTable[L]` equals the speed growth applied `L−1` times (floor) multiplied by every milestone whose level is `≤ L`, using `MulDivFloor` in ascending level order.
  - The effective speed composition order is `SpeedTable` → Research → Industry → Items (in `itemId` order) → Overdrive, clamped to `maxCombinedSpeedPpm` (R03).
- **Unlock kinds**: `always`, `researchPurchased {id}`, `factoryLevelAtLeast {level}`, `resourceProduced {id}`. Evaluation happens at event and command timestamps. Unlocks are monotonic within a run.
- **Effect kinds and targets**:

| `kind` | Value member | Allowed `target.kind` | Sources |
|---|---|---|---|
| `machineSpeed` | `speedPpm` / `valuePerLevelPpm` (> 10^6) | `allMachines`, `machine` | Research, Industry, Item |
| `machineOutput` | `factor` (integer 2–`maxCombinedOutputFactor`) | `machineType` (Items), `machine` | Item, Research |
| `salePrice` | `valuePerLevelPpm` (> 10^6) | `allResources`, `resource` | Research, Industry |
| `upgradeCost` | `valuePerLevelPpm` (10^5 … < 10^6) | `allMachines`, `machine`, `expansion` | Research, Industry |
| `startingCash` | `valuePerLevel` ($) | none | Industry |
| `startingStorageLevel` | `valuePerLevel` (levels) | none | Industry |

- **Item target** (see OD-1): `factory`, or `machineType` with a non-empty `allowedMachineIds`. `machineSpeed` and `machineOutput` Items apply to every machine of the chosen type. The conflict key is `(effect.kind, targetKey)`.
- **Prestige IP**: `IP = ISqrt(MulDivFloor(coefficient², FV, divisor))`, which equals `floor(coefficient × sqrt(FV / divisor))`. Only `kind: "sqrt"` is supported.

## Semantic rule codes (in addition to the v1 identity and structure codes)

| Area | Codes |
|---|---|
| Run start | `runstart.unlock.unknown`, `runstart.refund.out-of-range`, `runstart.speed-bound.invalid`, `runstart.output-bound.invalid` |
| Resources/recipes | `resource.price.negative`, `recipe.outputs.empty`, `recipe.amount.non-positive`, `recipe.duration.non-positive`, `recipe.resource.duplicate`, `ref.resource.unknown` |
| Machines | `ref.recipe.unknown`, `machine.max-level.invalid`, `machine.milestone.level-invalid`, `machine.milestone.duplicate`, `curve.growth.invalid`, `curve.overflow`, `speed.bound.too-fast` (any recipe < 0.1 s at `maxCombinedSpeedPpm`, R04) |
| Expansion | `level.sequence.invalid` (must start at 1 and be contiguous), `level.first-cost.non-zero`, `storage.capacity.non-increasing`, `factory.slots.non-increasing`, `factory.slots.too-many` (> 12) |
| Contracts | `contract.kind.unknown`, `contract.kind.resource-mismatch`, `contract.duration.required`, `contract.duration.forbidden`, `contract.reward.empty`, `contract.reward.item.unknown`, `contract.kinds.incomplete` (Plate, Gear and timed Bar must each appear) |
| Items | `item.tier.invalid`, `item.tiers.incomplete` (every tier 1–3 needs an Item obtainable from some contract reward), `item.target.invalid`, `item.duration.non-positive` |
| Research/Industry | `research.prerequisite.unknown`, `research.prerequisite.cycle`, `research.prerequisite.level-invalid`, `upgrade.max-level.invalid`, `industry.first-affordable.missing` (no level-1 cost ≤ IP at the threshold) |
| Effects | `effect.kind.unknown`, `effect.target.invalid`, `effect.value.out-of-range` |
| Overdrive/Prestige | `overdrive.value.invalid`, `prestige.threshold.invalid`, `prestige.curve.unsupported` |
| Offline policy | `policy.revision-1.required` (unchanged) |
| Presentation (Unity composition) | `presentation.key.unresolved` (unchanged, now over resources, machines and Items) |

## Evolution

- Adding IDs and retuning numbers is a new `contentRevision` inside version 2. It is save-compatible, because Save V2 rescales progress on load (R08). Removing or renaming an ID that a Save V2 references needs a save migration decision.
- A new effect kind, unlock kind, contract kind or curve kind, or a new required member, needs code and `contentSchemaVersion` 3.
