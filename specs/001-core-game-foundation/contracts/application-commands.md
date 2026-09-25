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
| `ResumeAndReconcile` | normalized return signal and one captured UTC instant | durable new checkpoint, factory snapshot, `OfflineReturnSummary`, `UsedFallback` | cold start selects a valid active checkpoint normally and reports `UsedFallback=false`; an in-process failed-suspension FR-037 fallback uses the last successful active checkpoint and reports true; with no successful checkpoint, retain the prepared in-process factory at zero credit and report false; uncertain promotion enters `ReturnBlocked` |
| `RetryReturn` | none while `ReturnBlocked` | reread store, durably reconfirm a matching active candidate before publication, or recompute/promote from source; reuse a representable captured return UTC, otherwise read and bind exactly one new UTC value | remains blocked with explicit failure if durable confirmation/outcome or a valid UTC reading cannot be resolved |
| `ReloadAuthoritativeGeneration` | none while `ReturnBlocked` | discard uncommitted candidate, validate and durably confirm the active generation, then publish it | remains blocked if active generation cannot be read, validated, durably confirmed, or published |
| `RecoverPreviousCheckpoint` | explicit action when a valid previous generation is advertised | validate previous, promote it as a new active generation, then publish | never silently rolls back; failure leaves current live/active state unchanged or remains blocked if invoked there |
| `ResetDevelopmentScenario` | explicit development action | fresh D001 snapshot | bootstrap/definition failure; current valid factory retained |

`AdvanceSimulation` is admitted only while lifecycle state is `Running`. `ReturnBlocked` admits only `RetryReturn`, `ReloadAuthoritativeGeneration`, status queries and an explicitly available `RecoverPreviousCheckpoint`. Development resource controls are visibly marked and target one explicit instance/buffer.

`LoadDevelopmentCheckpoint` exists solely for the explicit save/restore development journey in US3 and never computes offline production. Cold-start loading and a real lifecycle return go through `ResumeAndReconcile`. Selecting an active checkpoint on cold start is normal and does not set `UsedFallback`; only an in-process failed-suspension fallback sets it. `RetryReturn` must not treat a readable active manifest as proof of durability: when the active generation matches the pending candidate, it confirms that generation durably before publishing it and remains blocked if confirmation fails. `ReloadAuthoritativeGeneration` must likewise confirm that the validated active checkpoint is durable before replacing the blocked live state. On any successful world replacement, presentation cancels placement/pointer state and re-resolves selection by persistent ID or clears it.

For a representable backward return instant, reconciliation grants zero credit and promotes a candidate whose departure timestamp is `max(source departure, captured return)`, preserving a nondecreasing boundary. A missing or unrepresentable clock reading cannot create the required checkpoint and enters `ReturnBlocked` without promotion.

## Queries

| Query | Output | Consistency |
|---|---|---|
| `GetFactorySnapshot` | bounds, building projections, occupancy revision | One immutable application revision |
| `GetPlacementPreview` | requested footprint cells, validity, all rejection reasons | Does not reserve or mutate cells |
| `GetMachineInspection` | active state, process, progress, inputs, output/free capacity, blockers | Derived from same revision |
| `GetPersistenceStatus` | no-save/ready/writing/failure/recovered state | Contains no filesystem path exposed to UI |
| `GetLastOfflineSummary` | measured/credited/effective time, actual deltas, limiting reasons | Success summary only references durable result checkpoint |

## Core simulation preparation and capture

HD-018 (human decision, 2026-09-22) separates the warmed Core operation from allocating application results. `TryPrepare(source, out session)` validates/copies a factory into privately owned reusable state and returns a typed status. `session.Advance(elapsedTicks)` performs complete prevalidation, calculation, hidden staging and one publication with no warmed managed allocations; it returns `ProductionAdvanceStatus`. This status is not the public `AdvanceSimulation` command's complete immutable summary. `session.CaptureResult()` explicitly allocates a detached candidate/summary for the latest attempt. It is valid only after at least one advance; earlier capture throws `InvalidOperationException`. After a successful attempt it contains a candidate and summary. After a failed attempt it retains that failure status and contains neither candidate nor summary, even if an earlier advance succeeded; a later valid retry can then produce a fresh success. Captured successful results remain unchanged after later session reuse. The allocating static `ProductionSimulator.Advance(factory, ticks)` remains compatible and uses the same calculation path; it preserves the existing precedence where negative elapsed ticks return `InvalidElapsedTicks` before factory validation.

The application owns and retains the prepared session across consecutive advances. Compute the next application revision with checked arithmetic before any committing Core operation or replacement-state publication. On overflow, preserve the authoritative factory, prepared session and revision. Publish a successful candidate/session and incremented revision as one application operation. Capture the immutable command result before returning it or exposing another command; views never receive working graphs or borrowed reusable arrays. For supply/removal/activation commands, use a detached copy of current committed state and validate/prepare a replacement session before publication. Rejected commands preserve the old session. Placement, load and reset replace the session coherently with the world under the same prechecked-revision rule; a session from an older revision must never advance newer state. An internal allocating committed-state capture supports these operations without exposing mutable session state publicly.

Offline reconciliation uses a separate session and retains a detached candidate/result until durable promotion. No prepared graph or reusable summary is retained by persistence/UI. This changes neither Save V1 fields nor the durable-before-publication rule. Preparation and capture costs are measured separately from W001's warmed Core `Advance`; no zero-allocation claim is made for the complete application command or UI refresh.

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

A failed `RecordSuspension` follows FR-037: resume reads the last successfully written active checkpoint, reconciles from its stored departure time, and explicitly reports use of the fallback and loss of later unsaved live changes. If no successful checkpoint exists, the current in-process factory is retained with zero credit and `UsedFallback=false`. Cold start validates and reconciles from the active generation, but reports `UsedFallback=false` because it did not fall back from a failed in-process suspension.

## Traceability

- Placement: FR-001–006, SC-001
- Resources/production: FR-007–020, SC-002/003/006
- State ownership: FR-021, SC-006
- Save/load/offline: FR-022–027 and FR-032–037, SC-004/007/009/010
- Development journey: FR-028–031, SC-005/008
