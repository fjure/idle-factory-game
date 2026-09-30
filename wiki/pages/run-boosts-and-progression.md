---
title: Run boosts and progression
type: component
sources: [S018]
updated: 2026-09-30
---

# Run boosts and progression

T101 adds four commands. `ActivateItem` (`RunCommands.Items.cs`), `PurchaseResearch` and `PurchaseIndustry` (`RunCommands.Research.cs`), and `ActivateOverdrive` (`RunCommands.Overdrive.cs`) follow the pattern of [run commands and snapshots](./run-commands-and-snapshots.md). The snapshots `ItemsSnapshot`, `ResearchSnapshot`, `IndustrySnapshot` and `OverdriveStatus` live in `RunSnapshots.Items.cs`. (S018)

## Commands

- **Items**: a machine-type Item targets a machine type ID from `allowedMachineIds`, never an instance (OD-1). A factory Item takes no target, and its conflict key is `(kind, "factory")`. An active effect with the same key is rejected. The command consumes exactly one Item and adds the effect over `[t, t + duration)` in real UTC. At the exact end instant the same Item can be used again. `item.none`, `item.target.invalid` and `effect.conflict` keep the Item. (S018)
- **Research**: the checks run in order prerequisites, `level.max`, `rp.insufficient`. RP is spent in one checked operation, and the effect applies at once. The purchase ends in `EndCommand`, so a `researchPurchased` unlock holds immediately (see [run unlocks](./run-unlocks.md)). (S018)
- **Overdrive**: gated on the reserved unlock id `overdrive`. It sets `activeUntil` and `cooldownUntil = start + duration + cooldown`, and it cannot be reactivated while active or cooling down. `OverdriveStatus` adds a `Locked` stage to Ready, Active and CoolingDown. (S018)
- **Industry**: spends IP permanently. `machineSpeed`, `upgradeCost` and `salePrice` apply now. `startingCash` and `startingStorageLevel` apply only at the next run. Their values are additive: the catalog starting cash plus the sum of valuePerLevel × level, and storage level 1 plus the sum, clamped. Prestige reuses these numbers through the shared `NextRunStart` helper (see [run prestige transition](./run-prestige-transition.md)). (S018)
- Unknown Research or Industry IDs, and locked Overdrive, reject with `command.invalid`, following the T137 convention. (S018)

## Pre-check by trial

The pre-check derives speeds and cycles from live state. Item, Research and Industry therefore apply their change as a trial, check it with `ChangeFits`/`SettleFits`, and undo it inside `try/finally` before `BeginCommand`. The undo restores levels, RP, IP and the effect list exactly. A stale same-key effect left by a clock rebind is swapped out for the trial and restored, so a rejected command changes nothing. This is safe only because the session is single-threaded. A pure modifier computation that takes a hypothetical effect or level is the cleaner end state. (S018)

## Snapshots

`ResearchSnapshot` orders by prerequisite depth, then content order, as OD-8 requires. It reads `RunCatalog.ResearchInContentOrder`, because the ID dictionary loses the definition order. The first implementation sorted by ordinal ID and was corrected after review. `ItemsSnapshot` lists the target choices per machine type, with their conflict reasons. (S018)
