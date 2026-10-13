---
title: Run persistence
type: component
sources: [S018, S022]
updated: 2026-10-13
---

# Run persistence

T138 connects the [Save V2 document](./save-v2-document.md) to the durable generation protocol and ports the Feature 001 lifecycle from [local checkpoints and offline return](./local-checkpoints-and-offline-return.md) to the [run session](./run-session.md). (S018)

## Layers

- The Core port `IRunCheckpointStore` speaks `RunCheckpoint` (envelope, `RunState`, `PermanentState`, optional import record). Core has no file or Newtonsoft reference. (S018)
- `JsonRunCheckpointStore` in `Unity/Persistence` is a thin `SaveV2CheckpointCodec` over the unchanged T093 `CheckpointGenerationFiles<T>`, with the five `v2/` slot names from the contract. There is no second copy of the protocol, and nothing is written under `v1/` (R08). (S018)
- `ProbeFiles()` returns `NoFiles`, `PendingOnly` or `PublishedFiles`, because a read alone cannot tell "no files" from "data without a manifest". (S018)

## Lifecycle

`RunPersistence` keeps the Feature 001 states and admission table for `SaveNow`, `Autosave`, `RecordSuspension`, `ResumeAndReconcile`, `RetryReturn`, `ReloadAuthoritativeGeneration` and `RecoverPreviousCheckpoint`. Outside `Running` the session is lifecycle-blocked, so the bootstrap must call `ResumeAndReconcile` first. (S018)

- Departure is `max(clock, session timeline, active departure)`, so it never moves backwards. (S018)
- Every install re-reads the promoted generation from the store, re-anchors the session at the departure and settles once, which applies the R03 progress rescale. The first version installed the candidate's own objects; after a failed install a retry re-promoted mutated bytes and hit `SameGenerationMismatch`. (S018)
- While a prestige is pending, `SaveNow` returns `transition.pending`, autosave skips and a suspension writes nothing. A return installs the durable generation and drops the candidate without an award. (S018)
- Offline credit goes through `IRunReturnReconciler`. Since T105 the default is `OfflineRunReturnReconciler` over the [offline segmenter](./factory-run-bounds-and-offline-segmentation.md), run on a fresh read of the source per attempt; a reconciler must not return state shared with the source. `TryInstall` owns the settle after load, return and prestige promotion. The durable prestige commands are on the [prestige transition](./run-prestige-transition.md) page. (S018)
- An unreadable previous generation counts as "no previous" when choosing the next generation. Only a storage failure blocks. This deviates from Feature 001, where a torn previous slot would block every later save. (S018)

## Cold start

A data or manifest file without a valid active checkpoint takes the recovery path and never imports V1 (SEC-1). A lone pending file counts as no checkpoint file, so V1 import is allowed (OPS-2; owner decision 2026-09-30). T106 puts that decision into `ColdStart`, which the bootstrap calls instead of `ResumeAndReconcile`; see [run cold start and V1 import](./run-cold-start-and-v1-import.md) and [Save V2 and V1 import](./save-v2-and-v1-import.md). (S018)

## Pitfalls

- Session and store must share the same `RunCatalog` instance; a mismatch blocks every return as an invalid checkpoint. (S018)
- The W003-S device set under `Fixtures/SaveV2/device/w003-s/` is built from the test W003 catalog. Its IDs do not exist in production content, so T123 must rebuild or verify it before the emulator run. T124 confirmed it: the player rejected the set (`save.v2.invariant`). T245 added shipped-catalog sets `w003-s-shipped` and `prestige-ready`, which device runs must use. (S018)
- Safety-checkpoint advisories F-1 to F-6 are in [F003 security review](./f003-security-review.md). (S018)
- Each autosave reads both generations to pick the next number before the promote IO; measure it on the device. (S018)

## F005 layout (T302-T304, merged)

- `RunPersistence` is split behaviour-neutrally by line range into partials ColdStart and Transition, `RunPersistenceTypes.cs`, `OfflineReturn.cs` and an internal `SafeRunStore`. Architect minors: the `IsExpectedInfrastructureFailure` location and six near-identical `SafeRunStore` wrappers. (S022)
- `PublicApiSnapshotTests` compares Core's public surface with the committed baseline `core-public-api.txt`. Regenerate only with `IDLEFACTORY_REGENERATE_PUBLIC_API=1`; that run ends Inconclusive. It does not cover generic constraints or attributes. (S022)
- 36 command-path mutators are internal, with `InternalsVisibleTo("IdleFactory.PlayModeTests")`. Review found bypasses via `RunState.Effects` and `RunState.Priority` (`TryReorder`), both fixed; the old test `RunStateTests.A2` pinned `TryReorder` as public and was flipped. Restore paths stay public (`TrySetActive`, `TryReorderPriority`, `OverdriveState.TryRestore`); a single restore surface, dual-use setters and timer validation gaps go to T334 (#399). (S022)

## Still planned (F005)

A large live gap calls a `RecordSuspension(atUtcMs)` overload through `Admit`, and Save V3 writes only after a successful load or fresh start; a failed V3 write keeps the previous generation. See [Save V2 document](./save-v2-document.md) and [F005 plan overview](./f005-plan-overview.md). (S022)

## Autosave interval (F005 T306, merged)

`RunPersistence.Autosave` (desktop focus-loss safety checkpoint) has a minimum interval equal to `runStart.liveGapThresholdMs`, with a 60,000 ms fallback when null. It is measured on `IUtcClock`; a throttled call returns the no-op `AutosaveThrottled`, and a backwards or unreadable clock never throttles. Quit (`RecordSuspension`), `SaveNow` and prestige bypass the interval and restart it; a failed write does not. `RunLifecycleAdapter` catches every exception in the focus, pause and quit callbacks and the checkpoint write, so the run stays live and the next save succeeds. (S022)
