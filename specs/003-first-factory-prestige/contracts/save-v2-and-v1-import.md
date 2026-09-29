# Contract: Save V2 and V1 Import

**Consumers**: `SaveV2Mapper` and `JsonRunCheckpointStore` (Unity), `RunPersistence` and `V1Import` (Core application), and the fixtures.
**Normative for**: FR-024–FR-028, SC-005, and US4/US5 acceptance.

## Storage layout

- The generation protocol is unchanged from Feature 001 (pending → read back → retain previous → promote → verify). It is implemented once in `CheckpointGenerationFiles` (R08).
- `…/IdleFactory/Checkpoints/v2/`: `run-checkpoint.data-a.json`, `run-checkpoint.data-b.json`, `run-checkpoint.manifest-a.bin`, `run-checkpoint.manifest-b.bin`, `run-checkpoint.pending.json`.
- `…/IdleFactory/Checkpoints/v1/`: **read-only** in Feature 003. No Feature 003 code path opens a V1 file for writing. A test asserts this through a write-denying file-system fake and a byte-hash comparison.

## Envelope

- Strict UTF-8 JSON without a BOM and at most 1 MiB. Depth ≤ 16. Integers only. Unknown, duplicate and missing members are rejected.
- ≤ 64 machines, ≤ 256 entries per list, and IDs of ≤ 128 UTF-16 code units in canonical form.
- The mapper validates a fresh DTO and then builds the domain state through its constructors, so every invariant in [data-model.md](../data-model.md) is re-checked. On any failure the load is rejected (fail closed).

## Document (schemaVersion 2)

| Path | Type | Meaning |
|---|---|---|
| `schemaVersion` | `2` | Save contract version. It is independent of `contentSchemaVersion`. |
| `checkpointId`, `generation` | UUID, int64 ≥ 0 | Generation identity (Feature 001) |
| `departureUtcMs` | int64 | Departure boundary for offline return |
| `offlinePolicyRevision` | int32 (`1`) | Offline policy |
| `reconciledFromCheckpointId` | UUID, optional | Offline exactly-once link (Feature 001) |
| `contentRevision` | int32 | Diagnostic only; ID resolution is authoritative |
| `import` | optional `{ sourceSchemaVersion: 1, sourceCheckpointId, sourceGeneration }` | Present only on the checkpoint written by a V1 import |
| `permanent` | `{ ip, prestigeCount, industry: [{id, level}], items: [{id, count}] }` | Survives prestige |
| `run.runId`, `run.runNumber`, `run.previousRunId?` | UUID, int ≥ 1, UUID | Run identity and the prestige idempotency key |
| `run.cash`, `run.rp` | int64 `[0, 10^18]` | Currencies |
| `run.storage` / `run.factory` | `{ level, investedCash }` | Expansion tracks |
| `run.inventory` | `[{resourceId, units}]`, sorted, units > 0 | Global stock. Its sum is ≤ capacity. |
| `run.machines` | `[{instanceId, machineId, slot, level, isActive, progressTicks, cycleDurationTicks, investedCash}]` | `0 ≤ progress ≤ cycleDuration`; slots are unique and `< slotCount` |
| `run.priority` | `[instanceId]` | A permutation of the machines |
| `run.autoSell`, `run.unlocked` | sorted ID arrays | Settings and monotonic unlocks |
| `run.contracts` | `{ completions: [{offerId, count}], active?: {offerId, acceptedUtcMs, deadlineUtcMs?, requiredUnits} }` | At most one active contract |
| `run.research` | `[{id, level}]` | Purchased levels |
| `run.effects` | `[{itemId, effectKind, targetKey, startUtcMs, endUtcMs}]` | Unique `(effectKind, targetKey)` |
| `run.overdrive` | `{ activeUntilUtcMs?, cooldownUntilUtcMs? }` | |

## Load rules

1. The structural and semantic checks above run first. Then every ID must resolve in the current catalog, and a locked machine or resource must not be present unless the `unlocked` set lists it.
2. When the current effective `cycleDuration` differs from the saved `cycleDurationTicks`, progress is rescaled with `MulDivFloor` (R03). Content retuning is compatible.
3. A saved `effects` or `overdrive` instant that is already in the past at load is expired by the normal offline return. It is never dropped at parse time.

## Transitions that write

| Transition | Candidate | Idempotency | Publication |
|---|---|---|---|
| Autosave, suspension, manual save | Current session state, generation + 1 | New checkpoint ID each time | Not gated (already live) |
| Offline return | Reconciled state, `reconciledFromCheckpointId` = source | Feature 001 rules (captured return instant; retry re-reads durable state) | After verified promotion |
| Prestige | Next run: `runId′`, `previousRunId = runId`, `ip + award` | Durable active with `previousRunId == runId` ⇒ already done | After verified promotion |
| V1 import | Mapped run with `import` block, departure = return instant `r` | Durable V2 active exists ⇒ never re-import; failed promotion ⇒ re-promote the same bytes | After verified promotion |

## V1 import procedure (R10)

| Step | Rule | Failure code (shown with guidance) |
|---|---|---|
| 1 | Read the V1 active checkpoint with the unchanged `SaveV1Mapper` against the legacy import catalog (`contentSchemaVersion 1`) | `import.v1.invalid` |
| 2 | Reconcile offline with the unchanged Feature 001 `OfflineReconciler` at the captured `r`, in memory | `import.v1.clock` (missing or unrepresentable clock; retry allowed) |
| 3 | Map IDs: `dev.input → iron.ore`, `dev.output → iron.bar`, `dev.machine.basic → machine.smelter`. Any other ID fails. | `import.id.unsupported` |
| 4 | Stock: the sum over every building's input and output buffers, per mapped resource, checked | `import.arithmetic` |
| 5 | Machines: level 1, `investedCash` 0, `isActive` kept, `instanceId` kept. Slot and priority follow ascending `(anchorY, anchorX, instanceId)`. Progress is `MulDivFloor(p, smelterCycle, 50,000,000)`. | `import.slots.exceeded` (count > level-1 slots) |
| 6 | Total stock ≤ level-1 capacity | `import.capacity.exceeded` |
| 7 | Remaining fields take the fresh-run defaults. `iron.ore`, `iron.bar` and `machine.smelter` are added to `unlocked` (OD-5). | — |
| 8 | Serialize, promote to `v2/`, verify, publish once | `import.persistence` (retry re-promotes the same candidate) |

Failure leaves `v1/` byte-identical and publishes nothing. The player sees the failed rule and the recovery action set from OD-3.

## Fixtures

- The six committed V1 fixtures (`Tests/EditMode/Fixtures/SaveV1/01–06`) each import or fail with a pinned expected result. `06-w001-scale` exceeds the slot limit, so it must fail with `import.slots.exceeded`.
- New V1 fixtures cover: over-capacity summed stock, 5 machines, an unsupported ID, and a promotion interrupted between pending and manifest.
- New V2 fixtures: a fresh run, a mid-run with every family populated, pending prestige, and after prestige. The existing Save V1 pattern regenerates them in memory and compares bytes, and writing them is opt-in through `IDLEFACTORY_REGENERATE_SAVEV2_FIXTURES=1`.
