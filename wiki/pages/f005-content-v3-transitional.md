---
title: F005 content schema v3 and the transitional reward adapter
type: component
sources: [S022]
updated: 2026-10-14
---

# F005 content schema v3 and the transitional reward adapter

Batch 1 (T312, T313, T314) made content schema v3 real but deliberately transitional. Source: PRs #437-#439 and Issues #374-#376. (S022)

## What exists

- **Definition fields (T312):** immutable `ManualMiningDef`, `BonusDef`, `BuildPriceDef`, `ThresholdDef`, `MilestoneDef`, `ContractItemLadder`. They are optional constructor parameters with legacy defaults and nothing reads them yet. `TestCatalogBuilder` (fluent `Create`/`From`/`With*`/`Build`) is the way tests build catalogs; `RunTestCatalogs.IronLoop` goes through it. (S022)
- **Strict v3 reader (T313):** `contentSchemaVersion 3` is read strictly. The dispatcher accepts 1 or 3; anything else is `content.version.unsupported`. `objective` and `class` are checked before any `ContractOfferDef` is built. V2 device sets read a frozen r2 catalog; V1 import is proven on the v3 catalog. Shipped content is v3 at revision 2 with offer IDs `contract.plate`, `contract.gear`, `contract.timed-bar`. (S022)
- **Transitional reward adapter (history):** `RunRewardAdapter` computed Cash = ceil(quantity x base price x ratioPpm / 10^6) in checked long math; Item rewards come from the ladder. It and `ContractRewardDef.FromTable` were removed by T320 on 2026-10-14; the final formula is described in [F005 batch A economy and contracts](./f005-batch-a-economy-and-contracts.md). (S022)
- **Transitional golden (batch 1; T320 changed only `trace-r2.csv`, byte-identical under Mono):** `trace-r2.csv` was regenerated once and labelled (`trace-r2-transitional.md`). F003 pacing bands are reported, not asserted, until T341 restores them; conservation and SC-001 stay asserted. Consequence: chain run 1 eligibility is 6,548 s, 52 s under the ECO-2 band (Play-Tester F1). (S022)

## Validator (T314)

New structural rules: `sell.steps`, `live-gap.range` (1,000-28,800,000), `contract.bonus.fields` with unlock reachability, `contract.ratio.standing`/`.bonus`, `contract.quantity.cap`, `contract.produce.min`, `milestone.order`, `build.base.bound` (at most 10^12), `cost.floor`, `speed.level.dead` (now also dead `startingStorageLevel`), and `chainDominanceMarginPpm` > 0. (S022)

- **Two ratio layers:** the v2 `contract.reward.ratio` still runs; the new `contract.ratio.*` rules are gated on revision >= 3 and never double-report with it. (S022)
- **Pending owner decision:** the content constants live in the validator (`ProduceMinQuantity` 10, `CostFloorPpm` 500,000 meaning 0.5x, top storage capacity) and are documented in the guide and the v3 contract. They are not final. (S022)
- RuleCodes, the guide table and `RunContentSchemaConsistencyTests` must change together. (S022)

Related: [gameplay content v2](./gameplay-content-v2.md), [run contracts](./run-contracts.md), [F005 plan overview](./f005-plan-overview.md), [run balance policy and F005 matrix](./run-balance-policy-and-f005-matrix.md).

## Batch A validator additions

From revision 3: `decayPpm` 0 is rejected, `industry.ceiling.level` rejects an upgrade whose level 1 gains less than `minGainPpm`, `prestige.threshold.maxIndex` is 1..10,000, and the rules `start.reachable`, `manual.yield.bound`, `contract.quantity.capacity` and `ladder.uptime` apply; `cost.floor` and `speed.level.dead` fold the decayed Industry values. Details and open owner questions: [batch A](./f005-batch-a-economy-and-contracts.md), [open decisions](./f005-batch-a-open-decisions.md). (S022)
