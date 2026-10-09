# Session log contract

The session log is local only and write-only in the product. It is not game state and never part of the save.

## Format

- **Location**: `persistentDataPath/IdleFactory/SessionLog/segment-0.jsonl` and `segment-1.jsonl`. Each segment holds about 128 KB. When the active segment is full, the older one is truncated and becomes active. Total ≤ 256 KB.
- **Lines**: one JSON object per line, with members `{ "t": <record type>, "v": <schema>, ...fields..., "crc": <CRC32 hex of the line without crc> }`. On export, a torn or CRC-failing last line is skipped.
- **Schema**: `v = 1`. Record types and allowed fields follow [data-model.md](../data-model.md) › Session log. A writer must not emit any other member. `SessionLogSchemaTests` pins the allowlist by reflection over the event structs.
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
3. On Android it shows the path and offers the system share sheet through a `FileProvider` URI, subject to the plan-review decision in research F5-R12.
4. An empty log writes nothing and reports "nothing to export". A failure reports its reason category (no space, no permission, unknown), changes nothing and can be retried.

## Android backup

`SessionLog/` and `Exports/` are excluded in `fullBackupContent` (API ≤ 30) and `dataExtractionRules` (API ≥ 31). `scripts/check-release-build.ps1` verifies the exclusion in the merged manifest of an Android build. This is the release gate before any Android build distributed outside the development workstation (FR-031).
