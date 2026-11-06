---
title: Save V2 and V1 import
type: decision
sources: [S018]
updated: 2026-11-06
---

# Save V2 and V1 import

Feature 003 uses strict UTF-8 JSON Save V2 (`schemaVersion: 2`) under `Checkpoints/v2/`. A shared protocol owns generation durability; codecs own format serialization and validation. Runtime V1 import is read-only. `JsonCheckpointStore` retains its legacy writer for compatibility suites, but runtime never writes `v1/`. (S018)

## Shared checkpoint generation protocol

`CheckpointGenerationFiles<TCheckpoint>` executes pending → read-back → retain-previous → promote → verify. Its codec handles serialization, strict decoding and canonical-byte validation. The reader validates the highest-sequence valid manifest against data hash, ID and generation. `JsonCheckpointStore` delegates to it, preserving V1 file names and bytes. (S018)

`SaveV1CheckpointSource` holds only `ICheckpointStorageReader`; its default implementation reads without creating `v1/`. Missing files or directories mean absent; access and query errors become persistence failures. An inaccessible save therefore cannot be misclassified as empty and skipped for a fresh run. (S018)

Six V1 fixtures retain their selected generations and byte hashes; `SaveV1Mapper.cs` and the fixture files remain unchanged. (S018)

Save V2 stores each machine's progress together with the cycle length it was measured against. A load rescales that progress when content retuning changed the length. `contentRevision` is diagnostic only: ID resolution against the current catalog is authoritative. Unknown, duplicate or out-of-range data fails closed. (S018)

## Durable-before-publication transitions

Only offline return, prestige and V1 import must be durable before they are published. Ordinary commands rely on autosave: a crash rolls back a spend and its grant together, so no reward is ever duplicated. (S018)

- **Prestige**: every run has a `runId`. The next-run candidate records `previousRunId = runId`. On retry or restart, a durable active checkpoint whose `previousRunId` equals the in-memory `runId` shows the transition already happened, so there is no second IP award. Otherwise the same candidate bytes are re-promoted. (S018)
- **Offline return**: Feature 001 exactly-once rules, reused through the V2 codec. See [local checkpoints](./local-checkpoints-and-offline-return.md). (S018)

## V1 import procedure

At cold start the source is chosen in this order: a V2 active checkpoint, then a V1 import, then a fresh run. A `v2/` directory that holds data or manifest files without a valid active checkpoint takes the recovery path and never imports; a lone pending file counts as none ([cold start](./run-cold-start-and-v1-import.md)). The import runs these steps (S018):

1. It validates V1 with the unchanged `SaveV1Mapper` and the retained content-v1 development catalog. (S018)
2. It reconciles the offline interval with the unchanged Feature 001 reconciler, in memory only. (S018)
3. It maps the IDs: `dev.input` → Ore, `dev.output` → Bar, `dev.machine.basic` → a level-1 Smelter. (S018)
4. It sums the buffers into global stock.
5. It assigns slots and priority in `(anchorY, anchorX, instanceId)` order, keeping each instance ID and active state. (S018)
6. It rescales progress.
7. It rejects an unsupported ID, more machines than the level-1 slots, stock above the level-1 capacity, or an arithmetic failure. (S018)
8. It promotes one complete candidate that carries an `import` block. (S018)

A promoted V2 always wins the next start, so a V1 interval is never credited twice. The committed `06-w001-scale` fixture must fail with `import.slots.exceeded`. After a failed import the player gets Retry or a confirmed Start new run (OD-3, approved), and an imported run takes fresh-run defaults with Ore, Bar and Smelter unlocked (OD-5, approved). (S018)

Mixing builds loses progress in both directions (OD-7, accepted by T131), with no detection code; the manifest hash is not authentication (SEC-4). See [F003 security review](./f003-security-review.md). (S018)

Related: [factory run simulation](./factory-run-simulation.md), [run economy and prestige](./run-economy-and-prestige.md).
