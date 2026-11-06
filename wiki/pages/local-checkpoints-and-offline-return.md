---
title: Local checkpoints and offline return
type: decision
sources: [S001, S002, S003, S006, S018]
updated: 2026-11-06
---

# Local checkpoints and offline return

Save V1 is strict UTF-8 JSON below `Application.persistentDataPath`. A fresh DTO must pass required/unknown/duplicate-member checks, field-specific numeric validation, catalog resolution, buffer and progress invariants, and reconstructed occupancy before it may replace live state. Files over 16 MiB, more than 100,000 buildings, more than 256 entries per buffer and IDs over 128 UTF-16 code units are rejected as parser safety guards. (S001)

Writes use pending, active and previous complete generations. The adapter writes and flushes pending data, reads it through the same validator, retains the active generation as previous, promotes pending, then verifies the active checkpoint ID. A reconciled world or reward is published only after promotion. Previous-generation recovery is an explicit action that validates and promotes the previous data as a new active generation. (S001)

Every checkpoint records an offline-policy revision. Revision 1 defines numerator 4, denominator 5 and an eight-hour credited cap. Effective time is the floored rational product of the valid capped UTC interval, and [deterministic production](./deterministic-production.md) advances by that time. Policy changes require an explicit balance and compatibility decision. (S001)

An in-process return uses the checkpoint written by its matching departure. When that write failed, FR-037 requires explicit fallback to the last successful active checkpoint and its stored departure time; later unsaved changes are absent. A representable backward clock grants zero credit and promotes a nondecreasing departure boundary. Missing or unrepresentable time blocks return until retry obtains one valid reading or authoritative reload resolves state. (S001)

Save V1 stores departure time as Unix milliseconds, and the mapper rejects any instant it cannot represent losslessly. Raw `DateTimeOffset.UtcNow` carries 100 ns ticks on Windows and Android, so until T052 no checkpoint could be written in the running game. `DeviceUtcClock` now floors to whole milliseconds. Flooring is monotonic and idempotent, so the nondecreasing departure rule still holds. The mapper stays fail-closed. See [player-level test gaps](./player-level-test-gaps.md) for why tests missed this. (S001)

Retry rereads durable state first. It reuses a representable captured return instant for promotion retries; if none was valid, one new clock value is read and bound. This plus durable-before-publication prevents repository-controlled retries from applying one offline interval twice. (S001)

`RetryReturn` treats a readable active manifest as provisional: pending and reconstructed candidate matches must pass idempotent promotion with the matching checkpoint ID and durable confirmation before publication. Failed confirmation retains the candidate and blocked lifecycle. Cold-start selection reports `UsedFallback=false`; only an in-process failed-suspension FR-037 path reports true. (S002, S003)

An Android rotation is also a lifecycle transition that writes a departure and a return checkpoint. Its summary is withheld under [Android runtime rotation](./android-runtime-rotation.md) (R20). (S001) A short genuine return (HOME or focus cycle under 60 s, nothing to report) writes and reconciles normally but opens no modal summary; the result stays the last genuine summary (T246, see [Run panels](./run-panels.md)). (S018)

For current Feature 001 development acceptance, persistence/file-replacement evidence is required on Windows and, if an ARM64 Android Development Build launches through the owner's API 36 x86_64 emulator's `arm64-v8a` translation layer, on that emulator. The result is limited to that environment and does not establish behavior on physical Android hardware or release readiness; if the Player cannot run, keep Android runtime evidence unresolved. The owner deferred iOS device/build/performance evidence to open/blocked Issue #39; it remains unverified and is outside the current T041 prerequisite chain. See [Unity test and MAQA readiness](./unity-test-and-maqa-readiness.md) for device evidence limits. (S006)

Feature 003 plans Save V2 over the same protocol, with V1 made read-only; see [Save V2 and V1 import](./save-v2-and-v1-import.md). (S018)
