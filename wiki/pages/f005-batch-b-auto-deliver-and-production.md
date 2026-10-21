---
title: F005 batch B auto-deliver, production objectives and contract UI
type: component
sources: [S022]
updated: 2026-10-21
---

# F005 batch B auto-deliver, production objectives and contract UI

Covers T325, T326, T327, T328, T330, T363 (Issues #390, #391, #392, #393, #395, #466; PRs #461, #468, #470, #474, #475, #469). Later batch B pages: [Save V3 drift](./f005-batch-b-save-v3-drift.md), [live gap](./f005-batch-b-live-gap.md), [session log writer](./f005-session-log-writer.md). (S022)

## Domain
- **Timeline mapping (T325, #390):** `TimelineMapping` is the single live/offline time map. Legacy overloads are zero-tick settles routed through it; bit-identity was shown by a legacy-vs-mapping equivalence test on W003-B/S/R. (S022)
- **Auto-deliver (T326, #391):** scratch commit in the simulator, a reserve that keeps the contract's stock out of auto-sell, `SetAutoDeliver`. Expiry is `deadline + 1 ms`, evaluated before that instant's completions (owner, 2026-10-15; "+1 tick" in the spec meant the timeline millisecond, no 100 ns tick). (S022)
- **`SettleFits` must be pessimistic (QA fail twice).** #391: the reward must come from `ContractRewards.TryPlan` with 0/0 balances (not the current Cash), and neither reserve nor reward counts for a contract already past its deadline. #392: `AcceptContract` runs `SettleFits` while `Active` is still null, so a production offer that waiting output already fills must be counted in the accept pre-check. Near 10^18 an optimistic check lets AutoSell overflow after the mutation, which `EndCommand` forbids. (S022)
- **Production and bonus (T327, #392):** `ContractBoard.TryCountProduced` counts in O(1) inside the completion pass; the bonus triggers in the unlock pass with its window stamped once per firing pass; availability is lazy. `RunContractEventKind.Blocked` is raised once when a delivery or production completion becomes `value.limit`-blocked, nothing on unblock. A bonus window end above 10^18 drops the trigger and retries later. (S022)
- **Golden regeneration (parent, 2026-10-16):** with the bonus live, `trace-r2.csv` changed (880 to 898 lines) and was regenerated with a note in `trace-r2-transitional.md`; rejected: making policies skip bonus offers, which would hide the feature from the golden. (S022)
- **Content slice (T363, #466):** revision 2 gains a 60 s Plate cooldown, `contract.bonus-plate` (1.5x, 15 min window, trigger factory level 2) and `contract.produce-plate` (20 Plates, growth 1 %, 0.25x). Forced changes: growth 5 % to 1 % (`curve.overflow` at index 780), cooldown 120 s to 60 s (120 s moved run-1 eligibility outside the asserted 5,940-8,580 s guard, which was not widened). The bonus pays less than the standing 1.75x Plate offer. (S022)

## Equivalence tests (T328, #393)
53 cases (l)-(s): each compares a single advance, edge cuts, six seeded partitions and uneven frames by digest, with hand-computed oracles, a stepped `salePrice` conservation case and exact expiry boundaries E-1 vs E at several sub-ms phases, live and offline. Mutation checks in Unity (live expiry at deadline+0, sub-ms phase dropped, reserve off, floor to ceil) each failed tests. The oracle must not call the code under test (`SalePricing`). (S022)

## UI (T330, #395)
- Auto-deliver toggle (delivery only, Off after accept, hint "Applies to this contract only. Its stock is kept out of auto-sell."), "Produced N of M" progress that hides stock fields, cooldown and bonus lines with non-shrinking height, offline lines for AutoDelivered and ProduceCompleted with rewards. (S022)
- **Badge as label text:** a separate badge row changed the tab size (16 failures). "Contracts (n)" and "Contracts !" are the tab label's own text; only the compact dot is the `tab-contracts-badge` element in the tab corner. Accessible name "Contracts, n deliverable". (S022)
- Open until T340: the short-stock expiry line depended on panel memory; resolved by `AutoDeliverWasOn` on the expired snapshot, see [session log panel and UI](./f005-batch-b-ui-and-android.md). (S022)

Related: [batch A economy and contracts](./f005-batch-a-economy-and-contracts.md), [timeline mapping and auto-delivery (planned)](./f005-timeline-mapping-and-auto-delivery.md), [run contracts](./run-contracts.md).
