# Data Model: Core Game Foundation

**Source**: [spec.md](spec.md) · **Decisions**: [research.md](research.md) · **Contracts**: [contracts/](contracts/)

This model separates authored definitions, mutable authoritative state, durable checkpoint DTOs and presentation-only state. Types named here describe responsibilities and invariants; task generation decides individual source files.

## Numerical conventions

- Resource amounts and capacities are checked signed 64-bit integers but valid domain values are nonnegative whole units.
- Duration and progress use checked signed 64-bit 100 ns ticks. Valid elapsed/progress is nonnegative; recipe duration is positive.
- Offline efficiency and cap come from the checkpoint's supported immutable policy revision. Revision 1 is `4/5` after `288,000,000,000` credited ticks (eight hours). Integer division rounds a sub-tick fraction down once per return.
- Coordinates and footprint dimensions use checked 32-bit integers. Width and height are positive.
- Serialized UTC departure time uses Unix milliseconds; the persistence adapter converts the measured difference to Core ticks after checked range validation.

## Identifier value objects

| Type | Meaning | Validation |
|---|---|---|
| `ResourceId` | Stable authored material identity | Non-empty normalized string; case-sensitive ordinal comparison; development IDs use `dev.*` |
| `RecipeId` | Stable authored production identity | Same rules; unique in catalog |
| `BuildingDefinitionId` | Stable authored building type identity | Same rules; unique in catalog |
| `BuildingInstanceId` | Persistent placed-instance identity | Non-empty UUID text in Save V1; unique within factory |
| `CheckpointId` | Persistent checkpoint generation identity | Non-empty UUID text; changes on every promoted checkpoint |
| `OfflinePolicyRevision` | Save-compatible offline-policy identity | Positive Int32; Feature 001 supports revision `1` |

Definition IDs are never Unity object instance IDs, asset paths or localized names. Renaming an ID is a save-compatibility change.

## Authored immutable definitions

### ResourceDefinition

| Field | Type | Rule |
|---|---|---|
| `id` | `ResourceId` | Required and unique |
| `developmentLabel` | string | Required for F001 inspection; not gameplay identity |
| `presentationKey` | optional string | Unity adapter lookup only; no domain behavior |
| `isDevelopmentOnly` | bool | `true` for every F001 resource |

### ResourceAmountDefinition

| Field | Type | Rule |
|---|---|---|
| `resourceId` | `ResourceId` | Must resolve in catalog |
| `amount` | Int64 | Positive |

Duplicate resource entries within one definition are rejected after ID normalization. Bootstrap and persistence use the same rule; no implicit aggregation is permitted.

### OfflinePolicyDefinition

| Field | Type | Rule |
|---|---|---|
| `revision` | `OfflinePolicyRevision` | Required and unique; F001 supports `1` |
| `efficiencyNumerator` | Int64 | Positive and less than denominator; F001 is `4` |
| `efficiencyDenominator` | Int64 | Positive; F001 is `5` |
| `maxCreditedTicks` | Int64 | Positive; F001 is `288,000,000,000` |

For the initial Core Game Foundation baseline, the policy is authored as a ScriptableObject, validated once and copied into immutable Core data. After the separately accepted authoring migration, JSON supplies the same revision and values through validated DTOs. A balance change creates a human-approved revision and explicit compatibility decision; it does not mutate revision 1 in place.

### RecipeDefinition

| Field | Type | Rule |
|---|---|---|
| `id` | `RecipeId` | Required and unique |
| `inputs` | immutable list of `ResourceAmountDefinition` | At least one normalized positive entry |
| `outputs` | immutable list of `ResourceAmountDefinition` | At least one normalized positive entry |
| `durationTicks` | Int64 | Positive |
| `isDevelopmentOnly` | bool | `true` for D001 |

### BufferDefinition

| Field | Type | Rule |
|---|---|---|
| `capacities` | immutable map `ResourceId → Int64` | At least one declared resource; every capacity nonnegative; no duplicate ID |
| `role` | `Input` or `Output` | A buffer has one explicit role in F001 |

Capacity is per declared resource for this development model. This is not a final universal inventory/stacking rule.

### BuildingDefinition

| Field | Type | Rule |
|---|---|---|
| `id` | `BuildingDefinitionId` | Required and unique |
| `footprint` | `Footprint` | Positive width/height; D001 is 2×2 |
| `allowedOrientations` | set of quarter turns | D001 permits only zero; model remains orientation-ready |
| `recipeId` | optional `RecipeId` | Required for the D001 producer and resolvable |
| `inputBuffer` | optional `BufferDefinition` | Must cover all recipe inputs |
| `outputBuffer` | optional `BufferDefinition` | Must cover all recipe outputs |
| `presentationKey` | string | Required for the Unity placeholder lookup |
| `isDevelopmentOnly` | bool | `true` for F001 content |

### DefinitionCatalog

Immutable dictionaries for resources, recipes, buildings and supported offline-policy revisions. For initial Core Game Foundation acceptance, bootstrap constructs it from ScriptableObjects, validates the whole graph, then publishes it once. Runtime state holds only stable IDs and never mutates the catalog. Existing definition-ID semantics are immutable within Save V1; an incompatible recipe, footprint, capacity or policy change requires a new supported version/migration.

After the separately accepted Core Game Foundation baseline, the post-foundation technical migration changes the authoring input to versioned JSON, not this catalog contract. JSON is parsed into fresh content DTOs, checked against a repository-owned schema and semantic/cross-reference rules, then copied into the same immutable catalog before atomic publication. Existing Save V1 IDs and meanings remain stable. Unity ScriptableObjects may retain presentation-only references such as prefabs, sprites, audio, animation and VFX, keyed by stable IDs or presentation keys; they are no longer a gameplay/balancing source after migration. Future research, item, prestige and contract document sections are only an extensibility target, not new Feature 001 domain entities or mechanics.

## Factory geometry

### GridPosition

`x: Int32`, `y: Int32`; immutable value with checked footprint offset calculations.

### Footprint

`width: Int32`, `height: Int32`; both positive. `OccupiedCells(anchor, orientation)` returns every logical cell in deterministic row-major order. F001 fixed orientation does not rotate D001 but load validation rejects unsupported orientation values.

### FactoryBounds

| Field | Type | Rule |
|---|---|---|
| `origin` | `GridPosition` | Inclusive minimum |
| `width`, `height` | Int32 | Positive; product has no hard-coded future maximum |

All occupied cells must be inside bounds. Persistence applies a configurable safety limit before allocation; that input limit is not a gameplay maximum.

## Mutable authoritative state

### ResourceBufferState

| Field | Type | Ownership / invariant |
|---|---|---|
| `definition` | immutable `BufferDefinition` reference | Catalog-owned |
| `quantities` | map `ResourceId → Int64` | Building-owned; one entry for each declared resource; `0 ≤ quantity ≤ capacity` |

Operations return typed success/failure and are atomic:

- `CanAdd`, `TryAdd`
- `CanRemove`, `TryRemove`
- aggregate checked exchange used only by production completion

Negative requests, unknown resources, underflow, overflow or partial aggregate exchange are rejected without mutation.

### ProductionState

| Field | Type | Ownership / invariant |
|---|---|---|
| `recipeId` | `RecipeId` | Building-owned, must resolve and match definition |
| `progressTicks` | Int64 | Simulation-owned; `0 ≤ progress < recipe.durationTicks` |

Operational status is derived, not persisted:

- `Inactive`
- `Producing`
- `MissingInput` (zero or more resource details)
- `InsufficientOutputCapacity` (zero or more resource details)

Multiple blockers may coexist and remain inspectable. A partial cycle retains progress when inactive or blocked. Inputs remain stored until the cycle completes.

### BuildingInstanceState

| Field | Type | Ownership / invariant |
|---|---|---|
| `id` | `BuildingInstanceId` | Factory-owned and unique |
| `definitionId` | `BuildingDefinitionId` | Immutable after placement; must resolve |
| `anchor` | `GridPosition` | Factory placement operation writes atomically |
| `orientationQuarterTurns` | Int32 | Allowed by definition |
| `isActive` | bool | Application command changes explicitly |
| `inputBuffer`, `outputBuffer` | `ResourceBufferState` | Instance-owned; never shared |
| `production` | optional `ProductionState` | Instance-owned; simulation is progress writer |

### FactoryState

| Field | Type | Ownership / invariant |
|---|---|---|
| `bounds` | `FactoryBounds` | Factory-owned |
| `buildingsById` | map `BuildingInstanceId → BuildingInstanceState` | Unique identity and complete instances |
| `occupancy` | map `GridPosition → BuildingInstanceId` | Derived authoritative runtime index consistent with all footprints |

Placement is transactional: calculate cells → validate bounds/availability/definition → create instance → add instance and all occupied cells. On failure no identity is consumed as observable state and no occupancy/resource changes.

Save V1 does not store `occupancy`; load reconstructs it from validated instances, rejecting overlaps and out-of-bounds footprints. Factory-wide simulation is transactional: calculate and validate a complete candidate for every machine first, then replace the live factory once. Any arithmetic or invariant failure discards the candidate and leaves every live machine unchanged.

## Application-owned state and results

### FactorySession

Owns the current `FactoryState`, immutable `DefinitionCatalog`, simulation operation, persistence port, UTC clock port and lifecycle coordinator. It is the only application-level writer and serializes commands on the Unity main thread for F001.

### LifecycleState

```text
Bootstrapping → Loading → Running
Running ↔ Checkpointing
Running → Checkpointing → Suspended
Suspended → Reconciling → Running
Reconciling → ReturnBlocked
ReturnBlocked → Reconciling or Loading → Running
```

Rules:

- The initialization resume callback cannot reconcile an interval before bootstrap completes.
- Duplicate suspension or quit-after-suspension events reuse the exact checkpoint ID written by the first successful departure transition.
- If a departure write fails, resume explicitly falls back to the last successfully written active checkpoint and its departure time as required by FR-037; unsaved live changes after that checkpoint are not restored. If no successful checkpoint exists, the valid in-process live state remains with zero credit.
- Active simulation does not advance while `Suspended` or `Reconciling`.
- The reconciled candidate and consumed interval become observable only after durable promotion.
- A manual save or development-load failure returns to `Running` with the current live state and prior checkpoint unchanged.
- A reconciliation/promotion uncertainty enters `ReturnBlocked`; only retry or authoritative reload commands are admitted until the durable outcome is resolved.
- `RetryReturn` rereads the store and uses the return instant captured for that transition. An already-promoted candidate is published; otherwise the candidate is recomputed from the still-active source.
- `ReloadAuthoritativeGeneration` cancels the candidate and loads the active generation. `RecoverPreviousCheckpoint` is explicit and promotes the validated previous generation as a new active generation before publication.

### MachineInspectionSnapshot

Immutable projection with instance/definition/recipe IDs, active state, progress/duration, input available/required, output quantity/free capacity and all blocker details. Presentation cannot write through it.

### OfflineReturnSummary

| Field | Meaning |
|---|---|
| `outcome` | `Applied`, `NoCheckpoint`, `InvalidTime`, `PersistenceFailed`, `NoEligibleProduction` |
| `measuredTicks` | Nonnegative measured absence when valid |
| `creditedTicks` | `min(measured, 8 h)` |
| `effectiveTicks` | `floor(credited × 4/5)` |
| `resourceDeltas` | Whole-unit changes by resource and owner |
| `limitations` | Inactive/missing-input/full-output details |
| `sourceCheckpointId`, `resultCheckpointId` | Diagnostics/exactly-once trace |

The summary is a result projection, not saved gameplay authority. UI shows success only after `resultCheckpointId` is durable.

## Durable Save V1 DTO

The exact JSON contract is in [persistence-v1.md](contracts/persistence-v1.md). Required top-level state:

| Field | Type | Rule |
|---|---|---|
| `schemaVersion` | Int32 | Exactly `1` |
| `checkpointId` | UUID string | Required |
| `generation` | Int64 | Nonnegative and monotonically increases within the retained chain |
| `departureUtcUnixMilliseconds` | Int64 | Representable UTC instant |
| `offlinePolicyRevision` | Int32 | Resolves to immutable supported policy; exactly `1` in F001 |
| `reconciledFromCheckpointId` | nullable UUID string | Diagnostic predecessor for return processing |
| `factory` | Factory DTO | Required and wholly valid |

Factory DTO stores bounds and building DTOs. Each building stores identity, definition ID, anchor/orientation, active state, input/output quantities and recipe/progress. Capacities and footprints are reconstructed from unchanged definitions and compared with any contract-critical identifiers. Derived blockers, occupancy, selection, camera and views are not saved.

## Validation order for load/return

1. Enforce the V1 limits before expensive allocation: at most 16 MiB, 100,000 buildings, 256 entries per buffer and 128 UTF-16 code units per ID.
2. Strictly parse a fresh DTO; reject missing, duplicate, malformed or unsupported members/version.
3. Validate identifiers and field-specific checked numeric ranges: signed Int32 coordinates; positive dimensions; nonnegative quantities, progress and generation; positive policy revision; representable UTC.
4. Resolve every definition reference from the immutable catalog.
5. Reconstruct each instance and its buffers/progress.
6. Reconstruct occupancy; reject duplicate IDs, overlaps, illegal orientation or out-of-bounds cells.
7. Validate checkpoint UTC/generation metadata and resolve the exact supported offline-policy revision.
8. Produce a complete candidate factory without touching the live session.
9. For a return, calculate effective time and advance the candidate.
10. Durably promote the new checkpoint, then publish the candidate and immutable result summary.

Any failure before step 10 leaves the current live factory and prior successful checkpoint unchanged.

## Development fixtures

### D001

- Factory: origin `(0,0)`, size `16×16`
- Building definition: `dev.machine.basic`, footprint `2×2`, orientation `0`
- Resources: `dev.input`, `dev.output`
- Input capacity/initial quantity: `4/4`
- Output capacity/initial quantity: `1/0`
- Recipe: `dev.recipe.basic`, `2 dev.input → 1 dev.output`, duration `50,000,000` ticks

### D001 multi-cycle variant

Same definitions with output capacity `2`. Active ten seconds completes two cycles and produces state `input=0`, `output=2`, `progress=0`.

### W001 performance fixture

1,000 independent machines using the D001 `2 → 1 / 5 s` recipe: 500 active/unblocked, 250 output-blocked and 250 inactive. Each active machine starts with input `9,216` and output capacity `4,608`, exactly enough for 6.4 hours. The benchmark runs 100 untimed warmups and 500 independent timed samples, resetting outside each timed section and computing the resulting-state checksum afterward. It is generated in tests and is neither saved content nor a product factory limit.

## Presentation-only state

Camera pan/zoom, selected instance ID, placement mode, preview anchor/validity, pointer capture, open panels and transient messages are Unity-owned. None affects simulation or appears in Save V1. After factory replacement, selection is resolved against the new state by persistent ID or cleared; placement previews and pending commands are cancelled.
