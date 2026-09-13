# Contract: Application Commands and Queries

**Scope**: The boundary used by Unity input, UI, lifecycle and persistence adapters. Names are conceptual public contracts; implementation may refine C# naming without changing behavior.

## Contract rules

- Commands execute serially against one `FactorySession` on the F001 main-thread boundary.
- Each command returns a typed result. Expected rejection is not an exception and never partially mutates state.
- Views receive immutable snapshots/results and never receive mutable domain collections.
- Every command that takes an ID rejects empty, malformed or unknown IDs explicitly.
- Elapsed time and UTC values enter through dedicated ports rather than being read inside domain entities.

## Commands

| Command | Input | Success result | Rejection / failure |
|---|---|---|---|
| `PlaceBuilding` | definition ID, logical anchor, orientation | instance ID, occupied cells, new revision | unknown definition, illegal orientation, out of bounds, overlap; no mutation |
| `SetBuildingActive` | instance ID, boolean | inspection snapshot, new revision | unknown instance; no mutation |
| `AddResource` | instance ID, buffer role, resource ID, positive amount | resulting quantity/free capacity | unknown target/resource, invalid amount, overflow/capacity; no partial add |
| `RemoveResource` | instance ID, buffer role, resource ID, positive amount | resulting quantity/free capacity | unknown target/resource, invalid amount, insufficient stock; no partial remove |
| `AdvanceSimulation` | nonnegative Core duration | complete factory change summary, revision | negative/unrepresentable duration or any machine arithmetic failure; discard the full candidate and do not partially advance the factory |
| `SaveNow` | none; UTC obtained from port once | durable checkpoint ID/generation | validation/write/promotion failure; current factory and prior checkpoint retained |
| `LoadDevelopmentCheckpoint` | explicit development action while `Running` | active checkpoint snapshot loaded with zero offline elapsed time | absent/invalid active checkpoint returns to `Running` unchanged; cancels selection/preview only on successful publication |
| `RecordSuspension` | normalized lifecycle signal | durable departure checkpoint ID retained by the coordinator, or idempotent no-op | failure is recorded; matching resume uses FR-037's last-successful active checkpoint fallback, or retains live state with zero credit if none exists |
| `ResumeAndReconcile` | normalized return signal and one captured UTC instant | durable new checkpoint, factory snapshot, `OfflineReturnSummary` | uses the matching successful departure ID, otherwise the last successful active checkpoint and its departure time; uncertain promotion enters `ReturnBlocked` |
| `RetryReturn` | none while `ReturnBlocked` | reread store, then publish already-durable result or recompute/promote from source; reuse a representable captured return UTC, otherwise read and bind exactly one new UTC value | remains blocked with explicit failure if durable outcome or a valid UTC reading cannot be resolved |
| `ReloadAuthoritativeGeneration` | none while `ReturnBlocked` | discard uncommitted candidate and publish validated active generation | remains blocked if active generation cannot be validated |
| `RecoverPreviousCheckpoint` | explicit action when a valid previous generation is advertised | validate previous, promote it as a new active generation, then publish | never silently rolls back; failure leaves current live/active state unchanged or remains blocked if invoked there |
| `ResetDevelopmentScenario` | explicit development action | fresh D001 snapshot | bootstrap/definition failure; current valid factory retained |

`AdvanceSimulation` is admitted only while lifecycle state is `Running`. `ReturnBlocked` admits only `RetryReturn`, `ReloadAuthoritativeGeneration`, status queries and an explicitly available `RecoverPreviousCheckpoint`. Development resource controls are visibly marked and target one explicit instance/buffer.

`LoadDevelopmentCheckpoint` exists solely for the explicit save/restore development journey in US3 and never computes offline production. Cold-start loading and a real lifecycle return go through `ResumeAndReconcile`. On any successful world replacement, presentation cancels placement/pointer state and re-resolves selection by persistent ID or clears it.

For a representable backward return instant, reconciliation grants zero credit and promotes a candidate whose departure timestamp is `max(source departure, captured return)`, preserving a nondecreasing boundary. A missing or unrepresentable clock reading cannot create the required checkpoint and enters `ReturnBlocked` without promotion.

## Queries

| Query | Output | Consistency |
|---|---|---|
| `GetFactorySnapshot` | bounds, building projections, occupancy revision | One immutable application revision |
| `GetPlacementPreview` | requested footprint cells, validity, all rejection reasons | Does not reserve or mutate cells |
| `GetMachineInspection` | active state, process, progress, inputs, output/free capacity, blockers | Derived from same revision |
| `GetPersistenceStatus` | no-save/ready/writing/failure/recovered state | Contains no filesystem path exposed to UI |
| `GetLastOfflineSummary` | measured/credited/effective time, actual deltas, limiting reasons | Success summary only references durable result checkpoint |

## Typed outcomes

Minimum categories:

- `Success`
- `InvalidRequest`
- `NotFound`
- `OutOfBounds`
- `Occupied`
- `InsufficientResource`
- `InsufficientCapacity`
- `InvalidDefinition`
- `InvalidElapsedTime`
- `NoCheckpoint`
- `UnsupportedSaveVersion`
- `InvalidCheckpoint`
- `PersistenceFailed`
- `ReturnBlocked`
- `NoOfflineCredit`
- `LifecycleNoOp`

Technical diagnostics record operation/category/checkpoint or instance identifiers and exception context where appropriate. User feedback uses stable, non-technical messages and must not expose local file paths or raw save content.

## State publication

```mermaid
sequenceDiagram
    participant U as Unity adapter
    participant A as FactorySession
    participant C as Core candidate
    participant P as Checkpoint store
    participant V as View/UI
    U->>A: ResumeAndReconcile(captured return UTC)
    A->>A: Resolve matching departure ID or last-successful FR-037 fallback
    A->>P: Read that active checkpoint ID
    P-->>A: Valid DTO + generation + policy revision
    A->>C: Validate, map, advance effective time
    A->>P: Promote candidate checkpoint
    alt promotion succeeds
        P-->>A: Durable result checkpoint ID
        A-->>V: Publish factory + success summary
    else promotion fails
        P-->>A: Failure; prior checkpoint retained
        A-->>V: ReturnBlocked; failure/retry state only
    end
```

A failed `RecordSuspension` follows FR-037: resume reads the last successfully written active checkpoint, reconciles from its stored departure time, and explicitly reports use of the fallback and loss of later unsaved live changes. If no successful checkpoint exists, the current in-process factory is retained with zero credit. Cold start validates and reconciles from the active generation in the same way.

## Traceability

- Placement: FR-001–006, SC-001
- Resources/production: FR-007–020, SC-002/003/006
- State ownership: FR-021, SC-006
- Save/load/offline: FR-022–027 and FR-032–037, SC-004/007/009/010
- Development journey: FR-028–031, SC-005/008
