---
title: Run cold-start recovery
type: component
sources: [S018, S019]
updated: 2026-11-06
---

# Run cold-start recovery

The actions a player gets when the [run cold start](./run-cold-start-and-v1-import.md) is blocked. They follow the generation rules of [Save V2 and V1 import](./save-v2-and-v1-import.md), and the start-up surface that offers them is in [run panels](./run-panels.md). (S018)

## Actions

- `StartNewRunAfterBlockedImport` (OD-3) promotes a fresh run; if an imported V2 turns out to be durable, it is selected and reported as `SelectedDurableCheckpoint`. (S018)
- `DiscardDamagedSaveAndRestart` (owner decision 2026-10-01) closes the dead end of a damaged sole generation. It is allowed only from the cold start, in ReturnBlocked, when the active read is invalid or no manifest exists and no valid previous generation exists. It re-imports V1 once or starts a fresh run. An unsupported save version and storage failures are refused, so a save from another build is never overwritten. (S018)
- T141 removed the former limitation that an intact manifest with torn sole data could not be discarded. `SupersedeDamaged` re-derives every precondition inside the store under the operation gate and writes pending, then the unused data slot, then the alternate manifest with sequence + 1; the old manifest stays selected until the new one reads back. It refuses whenever any generation is valid, on a storage failure, an unsupported version or an unresolved ID. Existing `Publish` and `Promote` paths are unchanged. (S018)
- `save.v2.id.unresolved` (a content ID missing from this build) maps to `CommandOutcome.UnresolvedContent` with reason `checkpoint.content`, never to `InvalidCheckpoint`, so a save from another build is never offered for discard. The UI needs version-mismatch copy for it (T118). (S018)
- `V1Import` and its result types are `internal`; `V1ImportCodes` stays public for the bootstrap. `NextRunStart.CreateRun` is the single owner of fresh-run defaults for import, fresh start and prestige. (S018)

## Use previous save over a torn active generation (T195)

- `RunPersistence.RecoverPreviousCheckpoint` used to reject when the active generation could not be read. A torn active generation with an intact previous one therefore left the cold start blocked with Retry as the only action. The contract's cold-start case 2 already required the previous-generation recovery. (S018)
- T195 lets the recovery continue only when all four hold: the lifecycle is ReturnBlocked, the session never reached Running, the active read is exactly `InvalidCheckpoint`, and the previous generation reads and verifies. A save from another build is `UnsupportedSaveVersion` or `UnresolvedContent` and is never overwritten; storage failures still reject. (S019)
- It uses the unchanged store `RecoverPrevious` protocol: pending, data slot, alternate manifest with sequence + 1, and the old manifest stays selected until the new one reads back. The new generation reuses the torn active's data slot, because the previous generation occupies the other one; no bytes are repaired in place, and nothing valid is lost at any crash point. (S018, S019)
- Retry is exactly-once: `RecoverPreviousCheckpoint` and `RetryReturn` share one re-checked pending candidate. Discard stays refused while any generation is valid (SEC-1), and V1 is neither read nor written. (S019)
- A torn newest manifest is not a dead end: the store selects the older manifest and starts from the generation before it. (S019)
