---
title: Run session
type: component
sources: [S018]
updated: 2026-09-30
---

# Run session

T099 adds `RunSession` in `Core/Run/Application/`, the application layer between [run state domain](./run-state-domain.md), the simulator of [factory run simulation](./factory-run-simulation.md) and the Unity presentation. The commands are described in [run commands and snapshots](./run-commands-and-snapshots.md). (S018)

## Timeline and events

- The timeline is `anchorUtc + activeTicksSinceAnchor`. Whole milliseconds move the timeline and the sub-millisecond remainder is kept. `RebindAnchor` only moves forward; a backward clock is a no-op. (S018)
- `Advance` derives the boundary events from the state's timers (effect end, Overdrive end and cooldown end, contract deadline + 1 ms). It sorts them by `(SimTicks, kind, id)` and puts already-due boundaries at offset 0, which satisfies the simulator's caller contract. The event buffers are reused. (S018)
- An overflow returns `value.limit` and changes nothing, including the timeline and revisions. While the lifecycle is blocked, `Advance` returns `lifecycle.blocked`. (S018)
- A rebind does not settle. After a return or load, the caller must advance or settle so that due boundaries are processed. (S018)

## Revisions and commands

- `QuantityRevision` changes when an exact comparison against a pre-operation baseline finds different quantities. `StructureRevision` changes on detected timer changes (effect count, Overdrive status, active contract) or when the command passes `structural: true`. A command that forgets the flag leaves structure-keyed views stale; T137 passes it per command and tests the revision deltas. (S018)
- `BeginCommand`/`EndCommand` settles at the command timestamp with the simulator at offset 0: auto-sales first, then completions, then the run predicate. The settle runs after the command's own mutation, so on its own an overflowing settle is not atomic across the command. T137 closes this with the `SettleFits` pre-check before any mutation. (S018)

## Results and reads

- `RunCommandResult<T>` carries the 24 stable reason codes of the contract, mapped from `RunStateResult` and `RunAdvanceResult`. An internal `command.invalid` covers failures without a contract code and needs a generic UI fallback. The legacy `CommandResult` stays unchanged (GD-2). (S018)
- The hot reads `ReadCash`, `ReadRp`, `CopyInventory` and `CopyMachineStates` fill caller buffers (array or `Span`) and allocate nothing. `MachineStateView` reports slot, type index, level, active, running/paused/waiting, blocker (missing input, or storage full only when not even one unit fits), batch factor and progress in ppm. The view is rebuilt lazily after an operation; with 12 machines this is well inside the 1 ms host frame budget by reading, but not yet measured. (S018)
