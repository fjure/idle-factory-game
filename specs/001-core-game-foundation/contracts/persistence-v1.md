# Contract: Local Checkpoint JSON V1

**Media type**: UTF-8 JSON

**Location**: Application-owned filenames beneath Unity `Application.persistentDataPath`
**Authority**: A complete promoted generation only; scene hierarchy and PlayerPrefs are not gameplay state

## Files

The adapter owns fixed names equivalent to:

- active generation: `factory-checkpoint.json`
- previous complete generation: `factory-checkpoint.previous.json`
- pending generation: `factory-checkpoint.pending.json`

These are conceptual names; the implementation may use generation-suffixed files plus a small manifest if platform testing shows that to be safer. Save content cannot provide or alter paths.

## Required V1 shape

All fields shown below are required except explicitly nullable values. Unknown or duplicate JSON members are rejected so misspellings do not silently discard state.

```json
{
  "schemaVersion": 1,
  "checkpointId": "uuid",
  "generation": 1,
  "departureUtcUnixMilliseconds": 0,
  "offlinePolicyRevision": 1,
  "reconciledFromCheckpointId": null,
  "factory": {
    "originX": 0,
    "originY": 0,
    "width": 16,
    "height": 16,
    "buildings": [
      {
        "instanceId": "uuid",
        "definitionId": "dev.machine.basic",
        "anchorX": 0,
        "anchorY": 0,
        "orientationQuarterTurns": 0,
        "isActive": true,
        "inputQuantities": [
          { "resourceId": "dev.input", "quantity": 4 }
        ],
        "outputQuantities": [
          { "resourceId": "dev.output", "quantity": 0 }
        ],
        "production": {
          "recipeId": "dev.recipe.basic",
          "progressTicks": 0
        }
      }
    ]
  }
}
```

Definition-owned footprints, capacities, recipe inputs/outputs and durations are not duplicated in the checkpoint. The immutable catalog resolves them by stable ID. The checkpoint's `offlinePolicyRevision` resolves the immutable ratio and cap that govern its next return. Existing ID semantics and policy revision 1 are immutable within Save V1; incompatible content requires a new supported catalog/schema migration. Occupancy and blockers are derived during validation. Selection, camera, placement preview and UI state are never saved.

## Structural validation

- Reject more than 16 MiB input, more than 100,000 buildings, more than 256 entries in any buffer, or any ID longer than 128 UTF-16 code units before expensive allocation. These V1 parser guards are not gameplay limits.
- `schemaVersion` must equal `1`; older/newer values return `UnsupportedSaveVersion`. No migration is implemented yet.
- Every required member must be present with the expected JSON type. Null is accepted only where explicitly allowed.
- IDs are well-formed and unique in their scope. Resource lists contain no duplicate IDs.
- Integer parsing is checked and field-specific: `originX`, `originY`, `anchorX` and `anchorY` may be signed Int32; width/height and policy revision are positive; quantities, progress and generation are nonnegative; orientation must be supported; overflow and non-integral values are rejected.
- UTC milliseconds must be a representable instant.

## Semantic validation

1. Resolve every building, recipe, resource and `offlinePolicyRevision` in the current immutable definition catalog.
2. Validate bounds/dimensions and each definition's allowed orientation.
3. Validate exactly the resources declared for each input/output buffer and `0 ≤ quantity ≤ capacity`.
4. Validate recipe compatibility and `0 ≤ progressTicks < durationTicks`.
5. Reconstruct complete footprint occupancy; reject overlaps or out-of-bounds cells.
6. Validate checkpoint ID/generation/predecessor metadata and the representable UTC instant.
7. Build a fresh candidate factory. Do not deserialize over live objects.

No invalid field is clamped, dropped or silently reset.

## Promotion protocol

1. Produce a coherent Core snapshot and map it to a new DTO/generation.
2. Serialize to the pending path and close/flush the stream.
3. Read the pending bytes back through the same strict parser and semantic validator.
4. Retain the current active complete generation as previous.
5. Promote pending to active through the platform-tested file adapter.
6. Verify the active generation/checkpoint ID.
7. Only now return durable success and publish any reconciled candidate/award.

Failure at any step preserves a usable previous successful generation. Abandoned pending content is never authoritative. `RecoverPreviousCheckpoint` must be explicitly invoked, strictly validates the previous file, and promotes it under a new checkpoint ID/generation before publication; recovery never silently chooses a state-changing rollback.

If single-file replace/rename cannot meet interruption tests on a target platform, use two immutable generation files plus a separately recoverable generation selector. The observable contract remains the same.

## Return reconciliation and exactly-once rule

For source checkpoint `S` and one UTC reading `R`:

```text
measured = R - S.departureUtc
policy = catalog.Resolve(S.offlinePolicyRevision)
credited = measured > 0 ? min(measured, policy.maxCreditedTicks) : 0
effectiveTicks = floor(creditedTicks * policy.efficiencyNumerator / policy.efficiencyDenominator)
candidate = Advance(S.factory, effectiveTicks)
candidate.departureUtc = max(S.departureUtc, R)
candidate.offlinePolicyRevision = S.offlinePolicyRevision
candidate.reconciledFromCheckpointId = S.checkpointId
```

The candidate receives a new checkpoint ID/generation and must be promoted before it is exposed. A later boot reads the candidate and cannot replay `S`'s interval. A representable backward `R` uses zero effective ticks, keeps the nondecreasing source departure timestamp, promotes the no-credit candidate through the same transaction, and returns explicit feedback. A missing or unrepresentable UTC reading cannot create a valid required timestamp: no candidate is promoted and the application enters `ReturnBlocked`. Retry first rereads the store; it reuses a previously captured representable `R`, or reads exactly one new UTC value and binds it when the initial value was absent/unrepresentable. An invalid retry remains blocked. Authoritative reload is the alternative resolution.

An in-process return first uses the checkpoint ID successfully written by its matching suspension transition. If that write failed, FR-037 selects the last successfully written active checkpoint and its stored departure time; the result explicitly reports fallback and later unsaved live changes are not restored. If no successful checkpoint exists, the current in-process world remains with zero credit. Cold start validates the active generation. A retry rereads the store: if the result generation is already active it publishes that result; otherwise it recomputes from the still-active source with the return instant captured for the transition.

Exactly-once applies to repository-controlled lifecycle/retry behavior. It does not claim protection if a user externally replaces the local checkpoint or repeatedly advances the device clock.

## Failures and user outcomes

| Failure | Durable state | User-visible outcome |
|---|---|---|
| No checkpoint | None created by load | No-save message; development scenario remains usable |
| Unsupported/invalid/corrupt active | Active not applied; previous retained | Explicit failure and optional explicit recovery action |
| Pending write/validation fails | Existing active/previous unchanged | Save/return failed; retry available |
| Promotion interrupted | Recover active or previous complete generation | Explicit recovered/failure state; never partial world |
| Representable backward UTC | New no-credit candidate with nondecreasing source departure promoted | Explain invalid interval; valid factory retained |
| Missing/unrepresentable UTC | No promotion; enter `ReturnBlocked` | Explain failure; Retry Return or Reload Authoritative Save |
| Output/input blocks during return | Candidate commits only actual permitted cycles/progress | Summary names limiting conditions and actual deltas |

## Future migration entry

The loader dispatches on `schemaVersion`. V1 maps directly after validation. A future version adds a separately tested `Vn DTO → Vn+1 DTO` migration before domain mapping; no speculative migration classes or legacy fixtures are required in Feature 001.
