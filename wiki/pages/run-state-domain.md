---
title: Run state domain
type: component
sources: [S018]
updated: 2026-11-06
---

# Run state domain

T095 implements the mutable run state in plain C# under `Assets/IdleFactory/Core/Run/Domain/`, in the `IdleFactory.Core` assembly without engine references. It builds on the immutable content model from [gameplay content v2](./gameplay-content-v2.md) and is the state that the [factory run simulation](./factory-run-simulation.md) advances and the [run session](./run-session.md) exposes. (S018)

## Mutation rules

- **Validate, then commit.** Every mutator checks all of its conditions before the first write and returns a `RunStateResult`. Only `Ok` changes state. (S018)
- **Four inventory mutations.** `GlobalInventory` changes only through exchange, sale, delivery and import. `Apply` validates in one pass (per-resource stock, locked IDs, overflow, total against capacity) and commits in place in a second pass. A duplicate ID within one side of an exchange is rejected. (S018)
- **Composites live on `RunState`.** Operations that span several objects are `RunState.TrySell`, `TryBuildMachine`, `TryUpgradeMachine`, `TryRemoveMachine(id, out refund)` and `TryDeliverContract`. Each one computes every step, including cash, RP and Item overflow, before it writes. The first version sequenced sub-object calls, which could remove stock without crediting cash, and failed the architect review. The stock-only `GlobalInventory.TrySell` and `TryDeliver` stay public for tests; the application must use the composites. (S018)
- **Membership only through `RunState`.** `PriorityOrder.TryAppend` and `TryRemove` are internal, so the priority stays a permutation of the machines; `TryReorder` is public and all-or-nothing. (S018)

## Save V2 restore

Every persisted field has a validated restore path, and `RunState.Validate()` re-checks the loaded whole. The restore paths are: invested cash (`TryRestoreInvestment`), Overdrive instants (`OverdriveState.TryRestore`, cooldown − active = `CooldownMs`), Industry levels (`TrySetIndustryLevel`), research levels, effects (`TryAddEffect`, end − start = Item duration) and the active contract. Prerequisites, "machine type unlocked" and `runNumber = prestigeCount + 1` remain application or mapper rules. See [Save V2 and V1 import](./save-v2-and-v1-import.md) and the [Save V2 document](./save-v2-document.md). (S018)

## Speed and batching

`SpeedComposer` folds level speed (growth × milestones from the content table), research, Industry, Items in the caller's order, then Overdrive. Each step is `MulDivFloor(acc, f, 10^6)`, and the final clamp to `maxCombinedSpeedPpm` is defensive only. Level effects are capped at 1,024 levels. It also computes the OD-6 batch factor and cycle, the OD-9 partial count `k′ = min(k, k_in, k_cap)` and the progress rescale `p′ = MulDivFloor(p, D′, D)`, which never gains progress and keeps a waiting machine waiting. (S018)

The batched cycle satisfies `Tmin ≤ cycleDuration ≤ 2 × Tmin`. Equality with `2 × Tmin` is reachable through the ceiling rounding, and R03 was corrected from a strict bound. (S018)

## Determinism and allocations

Collections are lists or arrays in ordinal or slot order, and no rule enumerates a `Dictionary` or `HashSet`. `ConflictKey.GetHashCode` is randomized and must never order anything. The warmed hot paths do not allocate, and a `GcAllocationCounter` test pins that for T098: exchange, sale, delivery, compose, batch, rescale, and effect expiry by manual compaction instead of a capturing `RemoveAll`. (S018)
