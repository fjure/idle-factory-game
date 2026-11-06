---
title: F005 batch B Save V3 and content drift
type: component
sources: [S022, S029]
updated: 2026-11-06
---

# F005 batch B Save V3 and content drift

T334 (#399, PR #473), T335 (#400, PR #478), T336 (#401, PR #481). Contract: `specs/005-economy-contracts/contracts/save-v3.md`. (S022)

## T334: format and restore surface
- Reader accepts schema 2 and 3 via a version peek over root members only; the strict walk re-checks it; V3 members in a V2 file are rejected. The public `Serialize` always writes V3; an internal V2 overload exists only for fixture regeneration and refuses lossy input. (S022)
- `ContractBoard` gets a public restore surface (`CooldownStamps()`, `BonusOffers()`, `TryRestoreCooldown/Bonus/Produced/AutoDeliver/RunCompletionTotal`; API baseline +16 lines) instead of a Core-to-Unity `InternalsVisibleTo`. `RunState.Validate()` covers board consistency. (S022)
- **`runCompletionTotal` is persisted, never lowered:** restore as max(saved, sum of counts) and reject saved < sum, otherwise a dropped offer shrinks the total and an Item ladder index is granted twice. (S022)
- `Produced == Required` at load is accepted and completes at the first settle (parent, 2026-10-20). A consumed bonus cannot be used twice after reload; production progress survives reload; `LastOutcome` and the blocked flag stay transient. (S022)

## T335: drift on load, fail closed
- A contract-scoped drop reports `contract.dropped`; it applies to `activeContract`, `contractCooldowns`, `bonusOffers` and `completionCounts`. A drop never grants (SEC-8). It surfaces through the offline summary only (`RunOfflineSummary.ContractDropCode`, no UI copy yet). (S022)
- Interpretations (documented in the `SaveV2Mapper` class doc; accepted by the architect review, three with follow-ups on #401/#410): `run.unlocked` IDs of a removed offer are dropped only when a contract-scoped list of the same file names them; an objective change counts only with V3 evidence (`produced > 0` or `autoDeliver`) plus a differing `contentRevision` (otherwise the contract is kept); an unknown offer ID with an equal revision is still drift. (S022)
- **Bonus cross-field (V3 only):** an active bonus contract needs a triggered, unconsumed bonus record; a forged file without one is rejected (found by T334 QA). (S022)
- **Future-instant bound:** `readyAtUtcMs` and `windowEndUtcMs` must not exceed departure + cooldown or window + 7 d. Owner follow-up T345 caps `cooldownMs` and `windowMs` at 7 d so the bound is finite. (S022)
- Store-level refusal of `schemaVersion` 4 (no write, no V1 import) and fault injection for a failed first V3 write after a V2 load. (S022)

## T336: the W005-L fixture
`Fixtures/SaveV3/01-w005-l.json` (run 3, 12 slots, auto-deliver on `contract.gear`, auto-sell on Gear; SHA-256 ae620458...bf3e) is identical from the .NET harness and Unity, shipped as a StandalonePlayer Resources copy with parity and round-trip tests. Measurements: [performance and measurements](./f005-batch-b-performance-and-measurements.md). Adjacent fix: a test file using `UnityEditor` outside `#if UNITY_EDITOR` had broken every Windows test-player build on main. The offline reconciler writes the source's old `contentRevision` forward until the next autosave (harmless). (S022, S029)

Related: [Save V2 document](./save-v2-document.md), [run persistence](./run-persistence.md), [run contracts](./run-contracts.md).
