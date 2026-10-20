# Session log contract

The session log is local only and write-only in the product. It is not game state and never part of the save.

## Format

- **Location**: `persistentDataPath/IdleFactory/SessionLog/segment-0.jsonl` and `segment-1.jsonl`. Each segment holds about 128 KB. When the active segment is full, the older one is truncated and becomes active. Total ≤ 256 KB.
- **Lines**: one JSON object per line, with members `{ "t": <record type>, "v": <schema>, ...fields..., "crc": <CRC32 hex of the line without crc> }`. On export, a torn or CRC-failing last line is skipped.
- **Schema**: `v = 1`. Record types and allowed fields follow [data-model.md](../data-model.md) › Session log. A writer must not emit any other member. `SessionLogSchemaTests` pins the allowlist by reflection over the event structs.
- **Contract record** (T327): `event` is one of accept, deliver, autoDeliver, produceComplete, expire, abandon, and `blocked` (the enum `RunContractEventKind.Blocked`, raised once when the completion of the active contract becomes blocked by a numeric bound and never repeated while it stays blocked, AR2-4; `reward` is 0). The simulator-driven kinds come from the session's before/after comparison of the contract outcome and the blocked state, not from the simulator. data-model.md › Session log › Contract lists the same value set and needs `blocked` added (parent follow-up).
- **Mining record** (#385, 2026-10-14): `Mining { resourceId, counted, ignored, blocked, ore }` is raised once per `Mine` call, including a rejected call (`blocked` and `ignored` are the useful numbers). It is never one record per activation. The Unity adapter rolls the calls up per resource and minute.
- **Forbidden content**: free text, exception messages, file or directory paths, user names, device or hardware identifiers, advertising IDs, save bytes.
- **`sessionId`**: 128 random bits, new per app start, never persisted outside the log.

## Behaviour

- **Recording**:
  - It is on by default. The per-device setting `IdleFactory.SessionLog.Enabled` lives in `PlayerPrefs`, not in the save.
  - While the setting is off, nothing is recorded. Existing data stays until cleared.
- **Writes**:
  - Writes are buffered in memory and flushed at lifecycle points (start, focus-loss checkpoint, quit, prestige, offline return) and otherwise at most every 30 s of wall time.
  - There are no writes and no allocations inside `RunSimulator.Advance`.
- **Failures**:
  - Every write is isolated. An exception increments `droppedEvents` and never reaches the caller.
  - A failure never blocks or alters a save or a lifecycle callback.
- **Clear**: truncates both segments and writes a `Lifecycle{event: cleared}` record.

## Test seams

The writer takes an injected root directory, an injected `ISessionLogPrefs` and an injected file layer. Edit Mode and Play Mode tests use a temp root and a failing file layer. The StandalonePlayer log-failure test injects the failing layer through a test composition seam that is compiled into the test player. Tests never touch the real `persistentDataPath` log.

## Export

1. The export concatenates the valid lines in order into `persistentDataPath/IdleFactory/Exports/session-log-<yyyyMMddTHHmmssZ>.jsonl`.
2. On Windows it shows the folder path.
3. On Android it shows the path. The file is retrieved with `adb pull`, and there is no share action in F005 (D-M).
4. Exports are written to a temp file and renamed. At most 10 export files are kept.
5. Development builds with `IDLEFACTORY_DEV` use the root `persistentDataPath/IdleFactory-Dev/` (D-N).
6. An empty log writes nothing and reports "nothing to export". A failure reports its reason category (no space, no permission, unknown), changes nothing and can be retried.

## Android backup

`SessionLog/` and `Exports/` are excluded in `fullBackupContent` (API ≤ 30) and `dataExtractionRules` (API ≥ 31). `scripts/check-release-build.ps1` verifies the exclusion in the merged manifest of an Android build. This is the release gate before any Android build distributed outside the development workstation (FR-031).
