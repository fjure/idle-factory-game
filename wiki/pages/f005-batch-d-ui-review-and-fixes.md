---
title: F005 batch D UI review and fixes
type: decision
sources: [S022, S034]
updated: 2026-10-27
---

# F005 batch D UI review and fixes

T352 (#417, PR #499) was the independent UI/UX review of the F005 surfaces; T367 (#501, PR #504) fixed the findings the owner pulled into F005. (S022, S034)

## Review result (T352)
- 32 findings (R-01..R-32) against `contracts/ui-surfaces-f005.md` and QA-12; no contract invariant regressed; three majors: the hidden Industry reward (R-01, C-F1), the dead and alarm-styled offers list (R-06/R-07), orphaned focus after a machine fix button (R-20). (S034)
- Owner decision 2026-10-27: Q8 yes, one bundled fix task (T367) before the closeout tasks; Q1-Q7 as recommended: "$" for every amount and "Cash" only as the balance label (Q2); pre-stall warning (Q3) and store-full hint (Q7, pair with the auto-sell design) go to later features; touch and pointer cases for contracts, Overdrive, prestige, session log and Industry (Q4); "Item reward limit reached this run" until #387 is decided (Q5); tooltip-only accessible names accepted until an accessibility API exists, F004 adapter (Q6). (S022, S034)
- The reviewer may not approve the fixes; the same reviewer re-reviewed them. (S034)

## Fixes (T367)
- R-01: a line and a "Spend N IP in Industry" button on the Prestige panel and banner, plus an IP indicator on the Research tab. R-06/R-07: offer order and state. R-08: titles tell the three Plate offers apart ("Bonus: Plate contract", "Produce Iron Plate"). R-20: focus restored after a machine fix. R-23: producer filter applied in the view. R-02: "$" amounts. R-10, R-24, R-25, R-31 as listed in the review. (S034)
- Known leftovers: longer titles need a 200 % wrap check; `ItemRewardWithheld` re-derives the cap in the view, duplicating a Core rule, to revisit with #387. (S034)

## Pitfalls
- `:last-child` is not supported in Unity 6000.6 USS (R-27); style the last item with an explicit class. (S022)
- Touch and pointer cases are `[Values(true, false)] bool touch` tests on the real presenter; synthetic touch taps on some session-log buttons (Export, Clear) do not fire in the batch editor, so those assert targets only on touch. Real touch stays unverified in batch mode. (S022, S034)
- Accessible names remain strings and tooltips (R-16); see [run panel conventions](./run-panel-conventions.md).

Related: [batch C UI fixes](./f005-batch-c-ui-and-devtools-fixes.md), [batch D evidence](./f005-batch-d-final-evidence.md), [batch D workflow](./f005-batch-d-workflow-pitfalls.md), [run panels](./run-panels.md).
