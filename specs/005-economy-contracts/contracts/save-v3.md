# Save V3 contract

This contract extends `specs/003-first-factory-prestige/contracts/save-v2-and-v1-import.md`. It keeps the generation protocol, slot names, durability steps, strictness and V1 import unchanged.

## Version handling

- **Mapper**: the existing mapper stays one mapper, version-parametrized and optionally renamed to `SaveMapper` in a separate mechanical commit. Per-version member tables are chosen at the `schemaVersion` peek, and `ValidateBytes` compares against the version read. The existing `save.v2.*` diagnostic codes stay. Files stay in `Checkpoints/v2/` under the existing slot names.
- **Writer**: always writes `schemaVersion: 3`, and only after a successful load or a fresh start.
- **Reader**: accepts `2` and `3`. Any other version fails with `checkpoint.version`. Each version is strict: an unknown member, a missing required member or a wrong type fails with `checkpoint.invalid`.
- **Loading a V2 file**:
  - `autoDeliver = false`;
  - `produced = 0`;
  - no cooldowns;
  - no bonus state.

  The next write produces V3. A failed V3 write keeps the previous generation, and nothing is ever partially upgraded.
- **Downgrade**: not supported. A forward-compatibility test refuses `schemaVersion` 4 with `checkpoint.version`, with no write and no V1 import (SEC-14). Development builds with `IDLEFACTORY_DEV` use a separate root `IdleFactory-Dev/` (D-N). An F003 build reading a V3 file fails with `checkpoint.version` (development saves only; release note).

## V3 members (additions to the run object)

| Member | Type | Range | Notes |
|---|---|---|---|
| `activeContract.autoDeliver` | bool | strict `true`/`false` | Delivery objectives only; `false` for production objectives |
| `activeContract.produced` | long | [0, required] | Production objectives only; 0 otherwise |
| `contractCooldowns[]` | `{ offerId: string, readyAtUtcMs: long }` | ≥ 0; unique `offerId` | A `readyAtUtcMs` at or before the load's timeline start loads as ready |
| `runCompletionTotal` | long | ≥ sum of `completionCounts` | Run-wide completion index for the Item ladder; preserved when drift drops counts (SEC-8). On a V2 load it is derived from the sum of counts before any drop. |
| `bonusOffers[]` | `{ offerId, state: "triggered"\|"consumed", windowEndUtcMs: long }` | unique `offerId` | `windowEndUtcMs` is required for both states; a lapsed `triggered` entry never re-triggers |

The list is closed (FR-034). Adding a member needs a spec change and a new schema version.

## Content drift on load

Dropping is limited to contract-scoped lists. An `activeContract`, `contractCooldowns` entry, `bonusOffers` entry or `completionCounts` entry whose `offerId` does not exist in the loaded content is dropped. Every other unresolved ID stays fail-closed (`checkpoint.content`). Nothing is granted, and the load summary reports `contract.dropped` (an informational code, not a failure). An active contract is also dropped if its offer changed objective. All other validation is unchanged.

Three points are exact (#401, from the #400 review):

1. **Unlocked IDs of a removed offer.** A `run.unlocked` ID of a removed offer is dropped only when a contract-scoped list of the same file (`completionCounts`, `contractCooldowns`, `bonusOffers` or the `activeContract`) names that ID. The file itself then proves that the ID was an offer. An unlock ID that no such list names stays fail-closed (`checkpoint.content`).
2. **Objective change needs V3 evidence.** The reader detects a changed objective only from what the V3 file records (`produced` above 0 means a production objective, `autoDeliver` true means a delivery objective) and only when the file's `contentRevision` differs from the loaded content's. A change without evidence keeps the contract. Evidence that contradicts the offer with an equal revision is not drift and stays a `checkpoint.invalid` invariant failure (QA-11 `v3-autodeliver-on-produce`). A V2 file has no evidence and never drops a contract for its objective.
3. **An unknown offer ID is drift at an equal revision too.** The ID alone proves that the content lost the offer, whatever the revision says. Every drop is safe because a drop never grants a reward, an Item or Cash, so a forged revision gains nothing.

Note for the timing: the offline reconciler builds its candidate checkpoint with the source's old `contentRevision` (`OfflineReturn.cs`), so a checkpoint written by the return carries the old revision until the next autosave writes the live one. Drift detection is idempotent, so this is harmless, and the W005-L timing includes it (the fixture's revision equals the shipped one).

## Fixtures

- Every existing V2 fixture loads under the V3 reader: the device sets, W003, `prestige-ready` and the import outputs.
- `ContractBoard` restore setters: `TryRestoreCooldown`, `TryRestoreBonus`, `TryRestoreProduced`. These are public restore paths.
- A failed first V3 write after a V2 load leaves the V2 generation readable (fault injection).
- New V3 fixtures:
  - one per member;
  - each fail-closed case (bad bool, out of range, duplicate offer, missing `windowEndUtcMs`);
  - content drift;
  - the W005-L performance fixture: `Assets/IdleFactory/Tests/EditMode/Fixtures/SaveV3/01-w005-l.json` (T336), generated from code by `W005LFixtureTests`, with a StandalonePlayer `Resources` copy proved byte-identical by SHA-256 (`SaveV3PlayerFixtureParityTests`) and a read-then-write round trip through the reader. Its hash is in the fixture README and in `reviews/w005l-measurement.md`.
