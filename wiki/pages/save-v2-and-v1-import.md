---
title: Save V2 and V1 import
type: decision
sources: [S018]
updated: 2026-09-29
---

# Save V2 and V1 import

Feature 003 plans Save V2 as strict UTF-8 JSON with `schemaVersion: 2` under `Checkpoints/v2/`. The format-independent generation protocol (pending, read back, retain previous, promote, verify) moves out of the Save V1 store into one codec-parameterized `CheckpointGenerationFiles`. The V1 store becomes a **read-only** adapter over it. No Feature 003 path writes `v1/`, so a V1 generation is always recoverable and its bytes stay identical. (S018)

Save V2 stores each machine's progress together with the cycle length it was measured against. A load rescales that progress when content retuning changed the length. `contentRevision` is diagnostic only: ID resolution against the current catalog is authoritative. Unknown, duplicate or out-of-range data fails closed. (S018)

## Durable-before-publication transitions

Only offline return, prestige and V1 import must be durable before they are published. Ordinary commands rely on autosave: a crash rolls back a spend and its grant together, so no reward is ever duplicated. (S018)

- **Prestige**: every run has a `runId`. The next-run candidate records `previousRunId = runId`. On retry or restart, a durable active checkpoint whose `previousRunId` equals the in-memory `runId` shows the transition already happened, so there is no second IP award. Otherwise the same candidate bytes are re-promoted. (S018)
- **Offline return**: Feature 001 exactly-once rules, reused through the V2 codec. See [local checkpoints](./local-checkpoints-and-offline-return.md). (S018)

## V1 import procedure

At cold start the source is chosen in this order: a V2 active checkpoint, then a V1 import, then a fresh run. The import runs these steps (S018):

1. It validates V1 with the unchanged `SaveV1Mapper` and the retained content-v1 development catalog.
2. It reconciles the offline interval with the unchanged Feature 001 reconciler, in memory only.
3. It maps the IDs: `dev.input` → Ore, `dev.output` → Bar, `dev.machine.basic` → a level-1 Smelter.
4. It sums the buffers into global stock.
5. It assigns slots and priority in `(anchorY, anchorX, instanceId)` order, keeping each instance ID and active state.
6. It rescales progress.
7. It rejects an unsupported ID, more machines than the level-1 slots, stock above the level-1 capacity, or an arithmetic failure.
8. It promotes one complete candidate that carries an `import` block.

A promoted V2 always wins the next start, so a V1 interval is never credited twice. The committed `06-w001-scale` fixture must fail with `import.slots.exceeded`. Recovery after a failed import (OD-3) and the defaults of an imported run (OD-5) await the owner. (S018)

Downgrading to a Feature 002 build is unsupported: it would read the stale V1 and ignore `v2/`. (S018)

Related: [factory run simulation](./factory-run-simulation.md), [run economy and prestige](./run-economy-and-prestige.md).
