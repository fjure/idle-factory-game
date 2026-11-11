---
title: Run prestige transition
type: component
sources: [S018]
updated: 2026-11-06
---

# Run prestige transition

T102 implements Factory Value, the IP award and the in-memory part of the prestige transition. The economic rules are in [run economy and prestige](./run-economy-and-prestige.md), and the durable part is T105 (see the section below, [Save V2 and V1 import](./save-v2-and-v1-import.md) and the [Save V2 document](./save-v2-document.md)). (S018)

## Valuation

- `FactoryValuation` computes FV as cash plus stock plus the invested cash of machines, storage and factory, with checked arithmetic. Each resource's stock is valued with exactly the sale formula `MulDivFloor(units × basePrice, salePricePpm, 10^6)`, using the active Research and Industry modifiers, so FV equals the income of a real sale (ECO-8). Removed machines, RP, Research, contract rewards and spent Items add nothing. (S018)
- IP is `ISqrt(MulDivFloor(c², FV, divisor))` and is floored only in this step. The tests pin 10, 20, 30 and 100 IP at $10M, $40M, $90M and $1B. A build/remove cycle strictly lowers FV, also under discounts and at the highest refund (ECO-7). (S018)
- `NextRunStart` is the single helper for the next run's starting cash and storage level, shared by `IndustrySnapshot` and prestige. Starting cash from Industry is free and does not count as invested cash in the new run. (S018)

## Confirm, pending, promote

- `GetPrestigePreview` returns the FV breakdown, the award, eligibility with the missing value, and the reset and preserve lists as stable keys. (S018)
- `ConfirmPrestige` re-checks eligibility at the command instant and builds a deep, settled next-run candidate. It consists of a fresh content run with Industry starting effects, `ip += award`, `prestigeCount + 1` (so `RunNumber = PrestigeCount + 1`), unused Items kept, everything else reset, a new `runId`, and `previousRunId`. An active contract of either kind is dropped without reward or penalty, but its stock still counts in FV. (S018)
- The candidate is held as `RunSession.PendingTransition`, and the live run stays untouched. While it is pending, commands and `Advance` return `lifecycle.blocked`, and a second confirm returns `transition.pending` (QA-4). `PromotePrestige` publishes it and re-anchors the timeline monotonically; `CancelPendingTransition` drops it. The contract row for `ConfirmPrestige` was clarified to describe this split. (S018)

## Durable transition (T105)

`RunPersistence` owns the durable part; see [run persistence](./run-persistence.md). (S018)

- `CommitPendingTransition` (also `RetryPendingTransition`) first re-reads the durable active checkpoint. If its `previousRunId` equals the live `runId`, the transition already happened and is published without a second award; run 2 → 3 is not mistaken for a duplicate. Otherwise the candidate checkpoint is built once and re-promoted with the same ID and bytes, then a fresh read is installed and settled. (S018)
- With persistence attached, `PromotePrestige` and `CancelPendingTransition` route through an internal coordinator, so the UI cannot publish without the write. (S018)
- Cancel fails closed with `persistence.failed` when a promotion was issued and the store cannot be read, and returns `transition.committed` once the promotion is durable. The first version let cancel succeed, so a restart could load the cancelled prestige. No save may overwrite a durable successor, and recovery of the previous checkpoint is rejected while pending. (S018)
- The new run's departure is `max(ConfirmedUtcMs, active departure)`, even when the commit succeeds later; the offline cap bounds the effect. (S018)
- A return during a pending transition without a durable promotion credits nothing and rebinds the anchor, so the old run's timers do not pause across the absence. (S018)
