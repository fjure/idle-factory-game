---
title: Run cold start and V1 import
type: component
sources: [S018]
updated: 2026-10-08
---

# Run cold start and V1 import

> Gap: torn active plus intact previous generation blocks the cold start; see T195 in [run panels](./run-panels.md). (S018)

T106 adds the single cold-start entry point `RunPersistence.ColdStart(legacySource)` and the pure `V1Import`. It builds on [run persistence](./run-persistence.md) and the rules in [Save V2 and V1 import](./save-v2-and-v1-import.md); the read-only V1 side comes from [local checkpoints and offline return](./local-checkpoints-and-offline-return.md). (S018)

## Source selection

- A valid V2 active checkpoint is loaded and V1 is never read. Data or manifest files without a valid active checkpoint take the recovery path and never import (SEC-1). No files, or only a pending file, import V1 when one exists; otherwise a fresh run starts. (S018)
- A transient V2 read or probe failure blocks the start with `persistence.failed` and `ColdStartRetryRequired`. The first version fell through to the generic resume, which re-read, saw no checkpoint and started an unpersisted fresh run; the first autosave then made V1 unreachable for good. A retry re-runs the whole decision. (S018)
- Cold-start flags must be cleared on every exit. A flag left set after a successful import rejected the first resume after a suspension. (S018)

## Import

- `V1Import.Build` reads V1 through the unchanged `SaveV1Mapper` and legacy catalog, reconciles offline time in memory with the unchanged `OfflineReconciler`, maps Ore and Bar from the buffers and each machine to a level-1 Smelter in `(anchorY, anchorX, id)` order with `investedCash` 0 and proportional progress, and settles once at `r`. The install settle after promotion is then a no-op. (S018)
- IDs and the machine count are checked before the reconcile, because fixture `06-w001-scale` must fail with `import.slots.exceeded` rather than an ID error. A V1 read failure is `import.v1.invalid`, never a silent fresh run. Failures carry one of seven static codes and publish nothing. (S018)
- Exactly-once: a retry re-promotes the same candidate bytes, a restart selects the promoted V2, and a restart that finds only unpublished files recomputes with a new `r`. V1 is read through a write-denying fake in tests, and its directory hash never changes. (S018)

## Recovery actions

- `StartNewRunAfterBlockedImport` (OD-3) promotes a fresh run; if an imported V2 turns out to be durable, it is selected and reported as `SelectedDurableCheckpoint`. (S018)
- `DiscardDamagedSaveAndRestart` (owner decision 2026-10-01) closes the dead end of a damaged sole generation. It is allowed only from the cold start, in ReturnBlocked, when the active read is invalid or no manifest exists and no valid previous generation exists. It re-imports V1 once or starts a fresh run. An unsupported save version and storage failures are refused, so a save from another build is never overwritten. (S018)
- T141 removed the former limitation that an intact manifest with torn sole data could not be discarded. `SupersedeDamaged` re-derives every precondition inside the store under the operation gate and writes pending, then the unused data slot, then the alternate manifest with sequence + 1; the old manifest stays selected until the new one reads back. It refuses whenever any generation is valid, on a storage failure, an unsupported version or an unresolved ID. Existing `Publish` and `Promote` paths are unchanged. (S018)
- `save.v2.id.unresolved` (a content ID missing from this build) maps to `CommandOutcome.UnresolvedContent` with reason `checkpoint.content`, never to `InvalidCheckpoint`, so a save from another build is never offered for discard. The UI needs version-mismatch copy for it (T118). (S018)
- `V1Import` and its result types are `internal`; `V1ImportCodes` stays public for the bootstrap. `NextRunStart.CreateRun` is the single owner of fresh-run defaults for import, fresh start and prestige. (S018)
