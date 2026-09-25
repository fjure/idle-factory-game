---
title: Local checkpoints and offline return
type: decision
sources: [S001, S002, S003, S006]
updated: 2026-09-25
---

# Local checkpoints and offline return

Save V1 is strict UTF-8 JSON below `Application.persistentDataPath`. A fresh DTO must pass required/unknown/duplicate-member checks, field-specific numeric validation, catalog resolution, buffer and progress invariants, and reconstructed occupancy before it may replace live state. Files over 16 MiB, more than 100,000 buildings, more than 256 entries per buffer and IDs over 128 UTF-16 code units are rejected as parser safety guards. (S001)

Writes use pending, active and previous complete generations. The adapter writes and flushes pending data, reads it through the same validator, retains the active generation as previous, promotes pending, then verifies the active checkpoint ID. A reconciled world or reward is published only after promotion. Previous-generation recovery is an explicit action that validates and promotes the previous data as a new active generation. (S001)

Every checkpoint records an offline-policy revision. Revision 1 defines numerator 4, denominator 5 and an eight-hour credited cap. Effective time is the floored rational product of the valid capped UTC interval, and [deterministic production](./deterministic-production.md) advances by that time. Policy changes require an explicit balance and compatibility decision. (S001)

An in-process return uses the checkpoint written by its matching departure. When that write failed, FR-037 requires explicit fallback to the last successful active checkpoint and its stored departure time; later unsaved changes are absent. A representable backward clock grants zero credit and promotes a nondecreasing departure boundary. Missing or unrepresentable time blocks return until retry obtains one valid reading or authoritative reload resolves state. (S001)

Retry rereads durable state first. It reuses a representable captured return instant for promotion retries; if none was valid, one new clock value is read and bound. This plus durable-before-publication prevents repository-controlled retries from applying one offline interval twice. (S001)

`RetryReturn` treats a readable active manifest as provisional: pending and reconstructed candidate matches must pass idempotent promotion with the matching checkpoint ID and durable confirmation before publication. Failed confirmation retains the candidate and blocked lifecycle. Cold-start selection reports `UsedFallback=false`; only an in-process failed-suspension FR-037 path reports true. (S002, S003)

For current Feature 001 acceptance, persistence/file-replacement evidence is required on Windows and a supported physical Android device. The owner deferred iOS device/build/performance evidence to open/blocked Issue #39; it remains unverified and is outside the current T041 prerequisite chain. (S006) See [Unity test and MAQA readiness](./unity-test-and-maqa-readiness.md) for device evidence limits.
