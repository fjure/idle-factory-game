# Contract: Save V2 and V1 Import

**Consumers**: `SaveV2Mapper` and `JsonRunCheckpointStore` (Unity), `RunPersistence` and `V1Import` (Core application), and the fixtures.
**Normative for**: FR-024–FR-028, SC-005, and US4/US5 acceptance.

## Storage layout

- The generation protocol is unchanged from Feature 001 (pending → read back → retain previous → promote → verify). It is implemented once in `CheckpointGenerationFiles` (R08).
- `…/IdleFactory/Checkpoints/v2/`: `run-checkpoint.data-a.json`, `run-checkpoint.data-b.json`, `run-checkpoint.manifest-a.bin`, `run-checkpoint.manifest-b.bin`, `run-checkpoint.pending.json`.
- `…/IdleFactory/Checkpoints/v1/`: **read-only** in Feature 003. The runtime reads it only through `SaveV1CheckpointSource`, and no Feature 003 runtime path opens a V1 file for writing. A test asserts this through a write-denying file-system fake and a byte-hash comparison. `JsonCheckpointStore` keeps its V1 write API as a thin delegate over `CheckpointGenerationFiles` for the unchanged legacy suites only, which write to temporary roots; the bootstrap never references it (GD-1).

## Cold-start source selection

1. `v2/` holds a valid active checkpoint: load it and run the offline return.
2. `v2/` holds a data or manifest file but no valid active checkpoint: the Feature 001 recovery path (previous generation, then the blocked state with its actions). **Never import in this case** (SEC-1).
   - **Owner decision 2026-10-01 (dead end).** When the session is `ReturnBlocked` because `v2/` has files but neither a valid active nor a valid previous generation exists, the player gets a confirmed action, `RunPersistence.DiscardDamagedSaveAndRestart(legacySource)` (read model `CanDiscardDamagedSave`). It re-imports V1 when a valid V1 active checkpoint exists (the same import, once, a newly captured `r`), otherwise promotes a fresh run, and never touches V1. It is rejected with `discard.unavailable` in every other state, so SEC-1 holds whenever any valid V2 generation exists. A transient read or probe failure during `ColdStart` is not this case: it blocks with `persistence.failed` (`ColdStartRetryRequired`) and `ColdStart` is called again.
3. `v2/` holds no data or manifest file and `v1/` has an active checkpoint: V1 import (below). A lone `run-checkpoint.pending.json` counts as no checkpoint file: the first promotion was interrupted before any data slot was written, nothing was published, and the protocol discards the file, so the import is recomputed (the OPS-2 row below; owner decision 2026-09-30 after the T138 review). `IRunCheckpointStore.ProbeFiles()` reports this case as `PendingOnly`, and the bootstrap decides the import from it before `ResumeAndReconcile`.
4. Otherwise: a fresh run.

A confirmed **Start new run** after a blocked import (OD-3) promotes a fresh-run V2 checkpoint before publication, so the next start takes step 1 and V1 is not offered again (QA-8).

## Envelope

- Strict UTF-8 JSON without a BOM and at most 1 MiB. Depth ≤ 16. Integers only. Unknown, duplicate and missing members are rejected.
- The parser is a Newtonsoft `JsonTextReader` token walk (no reflection deserialization) that tracks the members seen per object and rejects a duplicate at token level, because Newtonsoft's default is last-wins. It rejects `1.0`, `1e3`, a BOM, more than 1 MiB, depth 17 and integers above 10^18 (SEC-3).
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
| `import` | optional `{ sourceSchemaVersion: 1, sourceCheckpointId (UUID), sourceGeneration }` | Present only on the checkpoint written by a V1 import |
| `permanent` | `{ ip, prestigeCount, industry: [{id, level}], items: [{id, count}] }` | Survives prestige |
| `run.runId`, `run.runNumber`, `run.previousRunId?` | UUID, int ≥ 1, UUID | Run identity and the prestige idempotency key |
| `run.cash`, `run.rp` | int64 `[0, 10^18]` | Currencies |
| `run.storage` / `run.factory` | `{ level, investedCash }` | Expansion tracks |
| `run.inventory` | `[{resourceId, units}]`, sorted, units > 0 | Global stock. Its sum is ≤ capacity. |
| `run.machines` | `[{instanceId, machineId, slot, level, isActive, progressTicks, cycleDurationTicks, investedCash}]` | `0 ≤ progress ≤ cycleDuration`; slots are unique and `< slotCount` |
| `run.priority` | `[instanceId]` | A permutation of the machines |
| `run.autoSell`, `run.unlocked` | ID arrays | Settings and monotonic unlocks. `autoSell` is in ordinal order. `unlocked` is unique and in unlock order (the initial set, then the order of unlocking, T140), not sorted, and may contain the reserved ID `overdrive` (below). |
| `run.contracts` | `{ completions: [{offerId, count}], active?: {offerId, acceptedUtcMs, deadlineUtcMs?, requiredUnits} }` | At most one active contract |
| `run.research` | `[{id, level}]` | Purchased levels |
| `run.effects` | `[{itemId, effectKind, targetKey, startUtcMs, endUtcMs}]` | Unique `(effectKind, targetKey)` |
| `run.overdrive` | `{ activeUntilUtcMs?, cooldownUntilUtcMs? }` | |

### Canonical form and interpretations (T104)

- **Optional members.** Optional members are always present and are `null` when absent: `reconciledFromCheckpointId`, `import`, `run.previousRunId`, `run.contracts.active`, `active.deadlineUtcMs` and the two Overdrive instants. A missing member is rejected like any other missing member.
- **Order.** Lists keyed by an ID are written in ordinal order and read strictly ascending, so a duplicate is rejected and there is one byte form. `machines` are ascending by slot. `priority` is any permutation of the machine instance IDs.
- **Strings.** Every string is a UUID or a catalog ID; `import.sourceCheckpointId` is a UUID too. UUIDs are lowercase, hyphenated and not all zero. Strings contain no JSON escape and no control character; a state that would need one is not written.
- **Codes.** Rejections use only these static codes: `save.v2.size`, `save.v2.encoding`, `save.v2.structure`, `save.v2.limit`, `save.v2.bounds`, `save.v2.version`, `save.v2.id.unresolved`, `save.v2.locked`, `save.v2.invariant` and `save.v2.crossfield`, plus `save.v2.write` when serializing.
- **Reserved unlock ID.** `run.unlocked` may contain the reserved ID `overdrive` (`RunState.OverdriveUnlockId`), the unlock of the Overdrive feature. It is the one unlocked ID that is not a resource, machine, offer, research or Item ID of the catalog, and it does not fail rule 1.
- **First settle.** A freshly created, imported or prestige-reset state is not a fixed point of the unlock pass until its first settle (`initialUnlocks` plus `always`, `researchPurchased` and `factoryLevelAtLeast` conditions unlock at the first advance or command). The mapper restores the saved state as it is and settles nothing, so a snapshot taken before the first settle can list a stale locked set, and it is written and read back unchanged. The session that owns the state settles first.
- **Rescale.** The R03 rescale of rule 2 needs the composed modifiers, so it belongs to the load and offline-return step. The mapper restores `cycleDurationTicks` and `progressTicks` exactly as saved.

## Load rules

1. The structural and semantic checks above run first. Then every ID must resolve in the current catalog, and a locked machine or resource must not be present unless the `unlocked` set lists it. The reserved ID `overdrive` is known without a catalog entry.
2. When the current effective `cycleDuration` differs from the saved `cycleDurationTicks`, progress is rescaled with `MulDivFloor` (R03). Content retuning is compatible.
3. A saved `effects` or `overdrive` instant that is already in the past at load is expired by the normal offline return. It is never dropped at parse time.
4. Cross-field bounds (SEC-2), each rejected with a corrupt fixture: `acceptedUtcMs` and effect `startUtcMs` are not after `departureUtcMs`; the Overdrive start is not persisted, so its active end is bounded relative to the departure instead: `activeUntilUtcMs − departureUtcMs ≤ MaxTimedDurationMs` (owner decision 2026-10-01 follow-up, so shortening the Overdrive duration in content cannot break a save with an active Overdrive); the persisted instants are authoritative like `requiredUnits` (owner decision 2026-10-01), so only `0 < endUtcMs − startUtcMs` (an Item effect), `0 < deadlineUtcMs − acceptedUtcMs` (an active contract) and `0 ≤ cooldownUntilUtcMs − activeUntilUtcMs` (Overdrive; a zero cooldown is valid content) are checked, each `≤ MaxTimedDurationMs = 604,800,000` (7 days, one named constant in Core that content validation shares), so no instant lies further in the future than that and a content retune of a duration or cooldown stays save-compatible while an effect, contract or Overdrive is active; `run.runNumber = permanent.prestigeCount + 1`; `run.previousRunId` is absent exactly on run 1. An active contract's `requiredUnits` is fixed at acceptance and is authoritative: it is only checked to be in `[1, 10^18]`, so retuning a quantity curve stays save-compatible (rule 2). Separately, an Item effect's `effectKind` and `targetKey` must be the Item's own (`factory` for a factory Item, an allowed machine type otherwise). That is an **invariant** (`save.v2.invariant`), not a rule-4 bound, and has no corrupt fixture. An `offlinePolicyRevision` the catalog does not have is rejected with `save.v2.version`.
5. Rejections carry only a static code and copy, never text taken from the file (SEC-5).

## Transitions that write

| Transition | Candidate | Idempotency | Publication |
|---|---|---|---|
| Autosave, suspension, manual save | Current session state, generation + 1 | New checkpoint ID each time | Not gated (already live) |
| Offline return | Reconciled state, `reconciledFromCheckpointId` = source | Feature 001 rules (captured return instant; retry re-reads durable state) | After verified promotion |
| Prestige | Next run: `runId′`, `previousRunId = runId`, `ip + award` | Durable active with `previousRunId == runId` ⇒ already done. While pending, `SaveNow` and a second confirm are rejected and autosave skips (run-application contract) | After verified promotion |
| V1 import | Mapped run with `import` block, departure = return instant `r` | Durable V2 active exists ⇒ never re-import. Failed promotion in the same session ⇒ re-promote the same bytes. Restart with only an unpromoted pending file ⇒ the protocol discards it and the import is recomputed with a newly captured `r`; nothing was published, so the V1 interval is credited once (OPS-2) | After verified promotion |
| Start new run (OD-3) | Fresh-run state from content | New checkpoint ID; V1 untouched | After verified promotion |
| Discard damaged sole save (owner decision 2026-10-01) | V1 import candidate (as above) or a fresh-run state, generation 1 | Only with no valid active and no valid previous V2 generation. Failed promotion in the same session ⇒ re-promote the same bytes; a restart after success selects the new V2 | After verified promotion |

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

**Implementation choices (T106).** (1) Order: the machine count (step 5) and the IDs (step 3) are checked on the V1 checkpoint before the legacy reconcile (step 2) runs, so an oversized or unsupported save is never simulated and `06-w001-scale` fails with `import.slots.exceeded` rather than an ID code; the stock sums (step 4) and the capacity check (step 6) follow the reconcile. (2) `RunPersistence.ColdStart(legacySource)` is the one cold-start entry point (it replaces `ResumeAndReconcile` at start-up); `StartNewRunAfterBlockedImport()` is the confirmed OD-3 action. A blocked import keeps the lifecycle in `Loading` with the failed code in `BlockedImportCode`. An unreadable, damaged or unsupported-version V1 save is `import.v1.invalid`; an absent V1 save is a fresh run. (3) Settle owner: `V1Import.Build` settles the candidate once at `r` before it is promoted; the install settle of `RunPersistence` is then a no-op on a fixed point. (4) The imported run's checkpoint and run IDs come from the checkpoint-ID source, so a retry in the same session re-promotes the same candidate (same ID, same bytes, same `r`) without reading V1 again.

## Fixtures

- The six committed V1 fixtures (`Tests/EditMode/Fixtures/SaveV1/01–06`) each import or fail with a pinned expected result. `06-w001-scale` exceeds the slot limit, so it must fail with `import.slots.exceeded`; the test first asserts its machine count is above the level-1 slot count (QA-9).
- New V1 fixtures: `07-over-capacity` (summed stock above level-1 capacity), `08-over-slot` (5 machines), `09-unsupported-id` and `10-corrupt` (`import.v1.invalid`). `import.v1.clock`, `import.arithmetic` and `import.persistence` are driven through the clock, catalog and store port fakes, and a promotion interrupted between pending and manifest is a fault-injection case, not a fixture (QA-1, QA-9).
- New V2 fixtures: a fresh run, a mid-run with every family populated (an active effect and a mid-cycle machine), pending prestige, after prestige, the three W003 workloads (`W003-B`, `W003-S`, `W003-R`, generated by `W003Workload`; plan.md, Performance now carries the SC-008 event bound on W003-B, so B is committed beside S and R) and one corrupt case per cross-field bound. The existing Save V1 pattern regenerates them in memory and compares bytes; drift without `IDLEFACTORY_REGENERATE_SAVEV2_FIXTURES=1` **fails** the test and never skips it (OPS-5). The README lists each fixture with its scenario and hash.
- Player tests cannot read the editor-only fixture folders, so the fixtures a player test needs are copied as Play Mode `TextAsset`s, and an Edit Mode test asserts hash equality with the originals (QA-2).
