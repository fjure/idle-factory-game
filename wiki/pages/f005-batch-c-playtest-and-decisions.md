---
title: F005 batch C Play-Tester report and decisions
type: decision
sources: [S022, S033]
updated: 2026-10-22
---

# F005 batch C Play-Tester report and decisions

T346 (#411): channel A ran the strategy matrix on revision 3, channel B one scripted Windows development-player session with the DevTools time scale. Neither is a human playtest (T353 stays open); feel statements are inferences. (S033)

## Owner decisions on #411 (2026-10-22)
- **C-F1** Industry section hidden at the bottom of the Research tab, 25 IP unspent: a later feature, not F005. (S033)
- **C-F2** store-full stall (auto-sell starves the chain and is off at every run start): adopt a **reserve mode** where auto-sell sells only the surplus above what machines and the active contract need. Because the spec lists "smart auto-sell" as excluded, the owner then moved it to a **later feature** (decision 2026-10-22, #411); no F005 task. D-L (auto-sell reserves stock of an active delivery contract) already covers the contract half. (S022, S033)
- **C-F3 to C-F8** (bonus card states, generic hint, Industry per-level decay wording, duplicate red triangles, locked-contracts wording) go to UI/UX review T352 (#417). (S033)
- **C-F7** run seconds on log event rows: idea for a later feature. **F-B5** stays a recipe note. (S033)

## Decisions on the batch-B report (#408, 2026-10-22)
F-B1/F-B7 and bonus appeal go to T352; F-B3 becomes T365; F-B2/F-B4 become T366; A-F1/A-F3 stay transitional until revision 3; spending IP is not F005. (S022)

## Decisions on policy r2 (#406, 2026-10-22)
Interpretations confirmed, bonus bound 1.0 to 2.0, tolerances released then re-derived, contracts-only stall is SC-002 evidence. See [policy r2](./f005-batch-c-policy-r2-and-gates.md). (S022)

## Lessons
- Policy r2 dumps surplus by `Sell`, so the runner never meets the C-F2 stall; runner gates cannot see this player-level defect. (S033)
- Usage limits killed workers again and resuming the same agent after renewing its leases worked. (S022)

Related: [batch A open decisions](./f005-batch-a-open-decisions.md), [content revision 3](./f005-batch-c-content-revision-3.md), [workflow pitfalls](./f005-batch-b-workflow-pitfalls.md).
