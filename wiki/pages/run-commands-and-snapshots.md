---
title: Run commands and snapshots
type: component
sources: [S018]
updated: 2026-09-30
---

# Run commands and snapshots

T137 adds the factory commands (`RunCommands.Factory.cs`, a `partial class RunCommands`) and the read-model snapshots (`RunSnapshots.cs`, a static partial class) in `Core/Run/Application/`. Both build on the [run session](./run-session.md) and the [run state domain](./run-state-domain.md). Later tasks add their commands and snapshots as further partial files; the contracts are described in [run contracts](./run-contracts.md). (S018)

## Command pattern

Every command runs in the same order: `lifecycle.blocked` first, then validation, then the settle pre-check, and only then the mutation in one checked transaction followed by the settle. A rejection changes nothing, and no command returns `value.limit` after it has mutated. (S018)

- **Settle pre-check (`SettleFits`)**: a conservative upper bound on the auto-sale income of the following settle. For each auto-sell resource it counts stock plus batch × amount × output factor of every waiting machine, whether active or not, using checked math. It can over-reject only within one worst-case sale of 10^18. Later commands that can complete waiting machines (Research, Items) need the same bound. (S018)
- **Structure flags**: `Build`, `Upgrade`, `Remove` and the expansions always change structure. `SetActive`, `ReorderPriority` and `SetAutoSell` report structure only when the value actually changed, and a test asserts the revision deltas of every command. (S018)
- **Codes**: `command.invalid` is used only for a stale or unowned instance ID and an ID collision, never for a contract case. An unknown machine type rejects as `machine.locked`, an unknown resource as `resource.locked`. (S018)
- **Remove** refunds `floor(investedCash × refundPpm / 10^6)`, frees the slot and closes the priority position. `refundPpm ≤ 999,999`, so build-then-remove always lowers Factory Value (ECO-7). Replacement is Remove then Build; there is no Replace command. (S018)
- **Build discount (owner decision 2026-09-30)**: `upgradeCost` effects on `allMachines` or `machine` also lower the build (level-1) cost, rounded up so a positive cost never becomes 0. `investedCash` stores the amount actually paid. (S018)

## Snapshots

- `RunFactorySnapshot`: slots including locked ones, machines, build and upgrade previews, refund and Factory Value delta, locked-type copy (`UnlockKind`, `UnlockId`, `UnlockLevel`), and both expansion tracks. (S018)
- `RatesSnapshot`: steady-state per-minute production, consumption and net of active machines at composed speed with batches (net before auto-sales); store usage; `SupplyCoveragePpm` for bottlenecks such as the 62.5 % Press supply of one Assembler; the blockers `Inactive`, `MissingInput` (auto-sell named as the cause, with the toggle as corrective action) and `StorageFull(netRequired, free)` only when not even one unit fits; and the last `k′` of `k` with its limiting input or the store. Rate text is floored to three significant digits, so it never overstates. (S018)
- `EconomySnapshot`: prices, stock value and auto-sell settings. (S018)
- Snapshots allocate per call. The UI must poll the revisions first and throttle refreshes that quantity changes trigger. `BuildProducer` actions still need a cross-check against `Unlocked` and `Affordable`. (S018)

## Unlocks

Nothing evaluated `UnlockCondition` when T137 merged, and commands and snapshots treat `RunState.Unlocked` as the authority. T140 added the evaluation in the simulator; see [run unlocks](./run-unlocks.md). (S018)
