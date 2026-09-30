---
title: Save V2 document
type: component
sources: [S018]
updated: 2026-09-30
---

# Save V2 document

T104 implements the Save V2 format in `Unity/Persistence/SaveV2Dtos.cs` and `SaveV2Mapper.cs`. The durable generation store and lifecycle build on it in T138 ([run persistence](./run-persistence.md)); promotion of transitions and V1 import follow in T105 and T106; see [Save V2 and V1 import](./save-v2-and-v1-import.md). (S018)

## Reader

- A lexical pre-pass runs before Newtonsoft. It enforces strict UTF-8 without BOM, at most 1 MiB, depth at most 16, canonical integers only (no fraction, exponent, leading zero, `-0` or `+`), and no escapes or control characters in strings. A `JsonTextReader` token walk then records the members seen per object and rejects duplicate, unknown and missing members at token level, because Newtonsoft alone would let the last duplicate win (SEC-3). (S018)
- The document may hold at most 64 machines and 256 entries per list, and IDs are at most 128 code units. Every exception except out-of-memory maps to a static code, and diagnostics never carry text from the file (SEC-5). (S018)
- The mapper builds the domain only through constructors and restore paths, then calls `RunState.Validate()`. Load rule 4 adds cross-field bounds: accepted, effect and Overdrive starts are not after the departure; durations match the content; `runNumber = prestigeCount + 1`; `previousRunId` is absent exactly on run 1 (SEC-2). The Item effect kind and target check is an invariant, not a rule-4 bound. (S018)

## Canonical form

Optional members are written as `null`, not omitted, so a missing member is always an error. `unlocked` keeps unlock order and may contain the reserved id `overdrive`. Other ID lists are ascending, machines are ordered by slot, and UUIDs are lowercase and hyphenated. `Serialize` reads its own bytes back and returns a static code instead of bytes when the state would be rejected on reload. (S018)

## Owner decisions

- The persisted `requiredUnits` of an active contract is authoritative and only range-checked, so content retuning stays save-compatible (load rule 2). A stricter "equals the current table" bound was removed. The rule-4 duration equalities (Item, offer, Overdrive cooldown) still reject a save after a retune while such a timer is active; that retune policy is an open owner question. (S018)
- `import.sourceCheckpointId` is a canonical UUID; V1 `CheckpointId` is always one. (S018)

## Fixtures and load-path obligations

- `Tests/EditMode/Fixtures/SaveV2/` holds 7 valid scenarios (fresh, mid-run, pending prestige, post-prestige, W003-B/S/R) and 9 corrupt ones, one per cross-field bound, with a README SHA-256 table. `SaveV2FixtureTests` regenerates them in memory and fails on drift unless `IDLEFACTORY_REGENERATE_SAVEV2_FIXTURES=1` is set. It never skips. (S018)
- The mapper restores `cycleDurationTicks` and progress exactly. The load path (T105) must rescale every machine to its effective cycle before any simulation, or a forged 1-tick cycle would survive. It must also clamp offline credit against a departure in the future, and settle once after load, import, creation and promotion. (S018)
