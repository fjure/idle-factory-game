---
title: Run prestige transition
type: component
sources: [S018]
updated: 2026-09-30
---

# Run prestige transition

T102 implements Factory Value, the IP award and the in-memory part of the prestige transition. The economic rules are in [run economy and prestige](./run-economy-and-prestige.md), and the durable part belongs to T105 (see [Save V2 and V1 import](./save-v2-and-v1-import.md) and the [Save V2 document](./save-v2-document.md)). (S018)

## Valuation

- `FactoryValuation` computes FV as cash plus stock plus the invested cash of machines, storage and factory, with checked arithmetic. Each resource's stock is valued with exactly the sale formula `MulDivFloor(units × basePrice, salePricePpm, 10^6)`, using the active Research and Industry modifiers, so FV equals the income of a real sale (ECO-8). Removed machines, RP, Research, contract rewards and spent Items add nothing. (S018)
- IP is `ISqrt(MulDivFloor(c², FV, divisor))` and is floored only in this step. The tests pin 10, 20, 30 and 100 IP at $10M, $40M, $90M and $1B. A build/remove cycle strictly lowers FV, also under discounts and at the highest refund (ECO-7). (S018)
- `NextRunStart` is the single helper for the next run's starting cash and storage level, shared by `IndustrySnapshot` and prestige. Starting cash from Industry is free and does not count as invested cash in the new run. (S018)

## Confirm, pending, promote

- `GetPrestigePreview` returns the FV breakdown, the award, eligibility with the missing value, and the reset and preserve lists as stable keys. (S018)
- `ConfirmPrestige` re-checks eligibility at the command instant and builds a deep, settled next-run candidate. It consists of a fresh content run with Industry starting effects, `ip += award`, `prestigeCount + 1` (so `RunNumber = PrestigeCount + 1`), unused Items kept, everything else reset, a new `runId`, and `previousRunId`. An active contract of either kind is dropped without reward or penalty, but its stock still counts in FV. (S018)
- The candidate is held as `RunSession.PendingTransition`, and the live run stays untouched. While it is pending, commands and `Advance` return `lifecycle.blocked`, and a second confirm returns `transition.pending` (QA-4). `PromotePrestige` publishes it and re-anchors the timeline monotonically; `CancelPendingTransition` drops it. The contract row for `ConfirmPrestige` was clarified to describe this split. (S018)

## Open for T105

The persistence layer must do the following. (S018)

- Write and verify the candidate checkpoint before promoting it. The idempotency key is the candidate's `previousRunId`.
- Implement `RetryPendingTransition`.
- Make `SaveNow` return `transition.pending` and make autosave skip while a transition is pending.
- Guard Promote and Cancel so that the UI cannot promote without the write.
- Prevent suspension or reconcile from crediting time to the old run while a transition is pending.
