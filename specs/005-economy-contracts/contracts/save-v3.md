# Save V3 contract

This contract extends `specs/003-first-factory-prestige/contracts/save-v2-and-v1-import.md`. It keeps the generation protocol, slot names, durability steps, strictness and V1 import unchanged.

## Version handling

- **Mapper**: the existing mapper stays one mapper, version-parametrized and renamed in place to `SaveMapper`. Per-version member tables are chosen at the `schemaVersion` peek, and `ValidateBytes` compares against the version read. The existing `save.v2.*` diagnostic codes stay. Files stay in `Checkpoints/v2/` under the existing slot names.
- **Writer**: always writes `schemaVersion: 3`, and only after a successful load or a fresh start.
- **Reader**: accepts `2` and `3`. Any other version fails with `checkpoint.version`. Each version is strict: an unknown member, a missing required member or a wrong type fails with `checkpoint.invalid`.
- **Loading a V2 file**:
  - `autoDeliver = false`;
  - `produced = 0`;
  - no cooldowns;
  - no bonus state.

  The next write produces V3. A failed V3 write keeps the previous generation, and nothing is ever partially upgraded.
- **Downgrade**: not supported. An F003 build reading a V3 file fails with `checkpoint.version` (development saves only; release note).

## V3 members (additions to the run object)

| Member | Type | Range | Notes |
|---|---|---|---|
| `activeContract.autoDeliver` | bool | strict `true`/`false` | Delivery objectives only; `false` for production objectives |
| `activeContract.produced` | long | [0, required] | Production objectives only; 0 otherwise |
| `contractCooldowns[]` | `{ offerId: string, readyAtUtcMs: long }` | ≥ 0; unique `offerId` | A `readyAtUtcMs` at or before the load's timeline start loads as ready |
| `bonusOffers[]` | `{ offerId, state: "triggered"\|"consumed", windowEndUtcMs: long }` | unique `offerId` | `windowEndUtcMs` is required for `triggered` |

The list is closed (FR-034). Adding a member needs a spec change and a new schema version.

## Content drift on load

Dropping is limited to contract-scoped lists. An `activeContract`, `contractCooldowns` entry, `bonusOffers` entry or `completionCounts` entry whose `offerId` does not exist in the loaded content is dropped. Every other unresolved ID stays fail-closed (`checkpoint.content`). Nothing is granted, and the load summary reports `contract.dropped` (an informational code, not a failure). An active contract is also dropped if its offer changed objective. All other validation is unchanged.

## Fixtures

- Every existing V2 fixture loads under the V3 reader: the device sets, W003, `prestige-ready` and the import outputs.
- `ContractBoard` restore setters: `TryRestoreCooldown`, `TryRestoreBonus`, `TryRestoreProduced`. These are public restore paths.
- A failed first V3 write after a V2 load leaves the V2 generation readable (fault injection).
- New V3 fixtures:
  - one per member;
  - each fail-closed case (bad bool, out of range, duplicate offer, missing `windowEndUtcMs`);
  - content drift;
  - the W005-L performance fixture.
