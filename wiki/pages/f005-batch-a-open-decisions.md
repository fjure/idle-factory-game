---
title: F005 batch A open decisions
type: decision
sources: [S022, S028, S033]
updated: 2026-10-22
---

# F005 batch A open decisions

Everything here is open for the owner. Parent interim decisions (2026-10-13/14, owner asleep) can be overruled; the Play-Tester report is a proposal, not a decision. Mechanics: [F005 batch A economy and contracts](./f005-batch-a-economy-and-contracts.md). (S022)

## Interim parent decisions awaiting the owner

- **`perRunCap`** = total ladder entries per run (interim); the alternative is per Item. (T320, S022)
- **`contract.quantity.capacity`** accepts indices 1..highest ladder index at revision >= 3; the final reachable bound waits for T341. (S022)
- **`var.contracts-slow`** is reported only until T341; inert cash/item reward kinds are documented as inert, a warning or rejection is open. (S022)
- **`start.reachable`** is kept literal (no store-capacity cap) because hand selling lets the window exceed one store. Open: model mine, sell, mine explicitly, or keep the level-1 store large enough. Also open: cap `maxShareOfMinerPpm` at 100 % instead of the structural 300 %. (T358, S022)
- **Market:** locked resources stay hidden (contract says a "Locked" row); "Need N" is the step size, not the shortfall; keep the cycling Amount button or move to a segmented selector after playtest. (T319, S022)
- Mine events are raised once per call including rejections. (S022)
- Carried to #410: `minGainPpm` unit unification, small additive upgrades cannot decay, schema still allows `maxIndex` up to 10^18. (S022)

## Play-Tester batch A (T342, proposal)

Two channels, neither a human playtest; feel statements are inference. (S028)

- Balance runner policy r1 exercises none of the batch-A verbs. Since batch 1, chain run 1 eligibility is 6,528 s (72 s under the ECO-2 floor of 6,600 s); ore-only still beats the chain in every run index. (S028)
- Revision-2 standing rewards are 1.75x, 1.70x and 1.67x of sell value, above the 1.5x cap (likely transitional, T345). All `cooldownMs` are 0 and no production-objective or bonus offer exists, so T321 and T322 are not player-visible. (S028)
- F6: a held Mine button gave no extra yield in the scripted session (may be the synthetic input; unverified). F7: the dev time-scale overlay does not render (no theme stylesheet on its PanelSettings). F8: `RunShellDocument.OnFocusIn` throws ArgumentException on focus changes. (S028)
- QA noted that flat early build prices are intended in revision 2 (`ownedGrowthPpm` 1,000,000), which explains finding F11; whether that stays is still the owner's call. (S022)

See [balance policy and matrix](./run-balance-policy-and-f005-matrix.md), [F005 plan overview](./f005-plan-overview.md).

Update 2026-10-22: the batch-A, batch-B and batch-C owner decisions are recorded in [batch C Play-Tester report and decisions](./f005-batch-c-playtest-and-decisions.md). (S033)
