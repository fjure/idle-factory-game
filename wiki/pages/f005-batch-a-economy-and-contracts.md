---
title: F005 batch A economy and contracts
type: component
sources: [S022]
updated: 2026-10-14
---

# F005 batch A economy and contracts

Batch A merged 2026-10-14 (main 77e04b0): T360, T323, T316, T331, T317, T324, T358, T319, T320, T359, T321, T322, T342 (Issues #429, #383, #379, #396, #380, #384, #385, #382, #387, #386, #388, #389, #407). Open owner decisions are on [F005 batch A open decisions](./f005-batch-a-open-decisions.md). (S022)

## Domain and commands

- **Hot loop (T360):** index-based completion pass (`RecipeIndex`, `GlobalInventory.TryExchangeIndexed`), bit-identical to the old path. W003-B on Windows Mono fell from 952.63 ms to 199.61 ms, emulator 431.64 ms, so no escalation before T326. New per-pass inventory work must stay on positions and indexed arrays, and every new mutation kind goes into `RecipeIndexEquivalenceTests`. See [bounds](./factory-run-bounds-and-offline-segmentation.md). (S022)
- **Sale pricing (T316):** `SalePricing.Value` = `MulDivFloor(units x basePrice, salePricePpm, 10^6)`, internal, checked, no clamping. Auto-sell and Factory Value route through it bit-identically. `chain.dominance` uses a non-decreasing reading with a strict terminal margin. (S022)
- **Sell (T317):** `Sell` and `SellAll` return `SellResult` (units, cash earned, cash after the settle). No partial mutation, full-stock sale is FV-neutral, the preview reports `lifecycle.blocked` first, and the sale event is raised once per command outside `Advance`. `sell.quantity-invalid` is a constant, not in `RunReasons.All`. (S022)
- **Build pricing (T323, T324):** `BuildPricing.Price` with discounts applied after the curve and rounded up; `Build(..., expectedPrice)` null, match or mismatch gives `build.price.changed`. T324 added `MachineTypeSnapshot.OwnedOfType`, `PriceCapped` (true only for a rising curve that reached its cap; flat Iron content reads false) and `RebuildPrice` (price at owned-1, for the Remove confirmation). (S022)
- **Prestige threshold (T331):** `PrestigeThreshold.TryFor` stops early on overflow. Owner decisions: from revision 3 `decayPpm` 0 is rejected (1,000,000 means no decay); `practicalCeilingLevel` is the last level with marginal gain >= `minGainPpm`; level 1 below `minGainPpm` is rejected (`industry.ceiling.level`); `prestige.threshold.maxIndex` is bounded to 1..10,000 and the 64-bit fast paths were dropped. (S022)
- **Manual mining (T358, T359):** `Mine` reports Counted, Ignored (cap) and Blocked (full store or limit). Capacity rejections now map to `storage.full`. The view reads a public read-only `RunSnapshots.Mining` (effective milli-yield, remainder, cap, `HasRoom`) instead of re-deriving the Research/Industry fold. `start.reachable` and `manual.yield.bound` (including the Industry speed term) validate content. (S022)

## Contracts

- **Rewards (T320):** `RunRewardAdapter` and `ContractRewardDef.FromTable` are gone. Cash is `SalePricing.Value` then one ratio floor; the modifier is read once at delivery. The Item ladder is run-wide (`runCompletionTotal`, pre-increment index, `perRunCap` = total entries per run, ordered by index then item id). Goldens: only `trace-r2.csv` changed, byte-identical under Mono. (S022)
- **Cooldowns and bonus (T321):** a cooldown is a UTC stamp `readyAt = end + CooldownMs` started by every ending; availability is evaluated lazily, nothing is scheduled. A bonus offer is single use (accept, abandon, re-accept fails). `ContractBoard.Version` counts every state change. Reasons `contract.cooldown`, `contract.bonus.unavailable`, `contract.capacity` stay outside `RunReasons.All`. (S022)
- **Production objectives (T322):** `ActiveContract.Produced` is internal and board-owned (0 at accept, cleared with the contract, reset with the run). `DeliverContract` rejects a production objective with `command.invalid`; accept skips the capacity check for it. Nothing counts into `Produced` before T327; trades and mining never touch it. The restore path is deferred to T334 (#399), together with persisting `runCompletionTotal` on its own. (S022)

## UI surfaces

Market Sell rows (T319: cycling Amount 1/10/100/All, All confirmation, stacked full-width controls so labels break only at word boundaries), the ore vein (T359: one-line pulse, short "Store full" reason, tooltip carries the long copy) and build price rows with Rebuild price (T324). Accessible names use the repo tooltip convention because Unity 6000.6 UI Toolkit exposes no accessibility API here (VEIN-8 satisfied within that limit, carried to #417). See [run panels](./run-panels.md). (S022)
