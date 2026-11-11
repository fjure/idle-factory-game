---
title: Run unlocks
type: component
sources: [S018]
updated: 2026-11-06
---

# Run unlocks

T140 closes the FR-013 gap that the T137 review found: nothing evaluated `UnlockCondition` until then. The owner decided on 2026-09-30 that the simulator owns the evaluation. See [run commands and snapshots](./run-commands-and-snapshots.md) and [factory run simulation](./factory-run-simulation.md). (S018)

## Evaluation

- `RunSimulator.ProcessTimestamp` runs an unlock pass after completions and sales and before the run predicate. When something unlocks, completions and sales run again, and the loop repeats until nothing unlocks. Chains therefore resolve at one instant, and a waiting machine uses a newly allowed resource at once. (S018)
- The pass evaluates resources, machine types, contract offers and Overdrive. The candidates are fixed at construction in ordinal order per family, then Overdrive. `ResearchDef` has no unlock condition, so Research is gated by its prerequisites only. (S018)
- The kinds are `always`; `researchPurchased` (level at least 1); `factoryLevelAtLeast`; and `resourceProduced`, which holds from the first completion that adds units in this run. Stock alone, for example imported stock, does not count. (S018)
- Unlocks and the inventory reshape they cause are staged and published atomically with the advance, so a rejected advance unlocks nothing. `RunSession` bumps `StructureRevision` when the unlocked set grows. The set only grows within a run. (S018)

## Pitfalls

- **Evaluation points**: the pass evaluates every locked candidate at the first timestamp of an advance or command settle, and later only after a first-time production. This is exact only because research and factory levels change solely in commands that end with `RunSession.EndCommand`. A future mutation outside a command settle would miss unlocks. (S018)
- **Reserved id**: Overdrive is stored in `RunState.Unlocked` under `overdrive`. Nothing yet rejects content definitions with that id, and Save V2 must accept it. Both are follow-ups for T097 and T104/T105. (S018)
- **Fixed point**: every state an advance publishes is a fixed point, so no produced set is persisted. A freshly created, imported or prestige-reset state is not a fixed point until its first settle, so snapshots taken before that may show a stale locked set. (S018)
- **Unreachable chains**: a `resourceProduced` condition on a never-produced resource, or a cycle, stays locked forever without an error. A static reachability check belongs in the content validator, not at runtime. (S018)
- **Pass bound**: the OD-2 bound of N+1 passes per timestamp becomes N+1+U with U unlock rounds at that timestamp. Tests that assert N+1 hold only while no unlock happens mid-advance. (S018)
