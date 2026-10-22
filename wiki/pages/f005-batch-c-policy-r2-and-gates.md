---
title: F005 batch C policy r2 and FR-024 gates
type: component
sources: [S022, S026, S031]
updated: 2026-10-22
---

# F005 batch C policy r2 and FR-024 gates

T341 (#406) built reference policy r2 under `Assets/IdleFactory/Tests/EditMode/Balance/`; no content or production code changed. Design: [balance policy and matrix](./run-balance-policy-and-f005-matrix.md). (S031)

## Owner-confirmed interpretations (Q10, 2026-10-22)
The literal text of `balance-policy-r2.md` broke the policy in several places; the owner confirmed these readings and the document is amended. (S026, S031)
- **Storage blocker is read on arrival**, before the look's own `Sell`. Read after the sale it never fires at long look intervals; that was also the cause of policy r1's slow casual run 2 (storage stuck at level 1). (S031)
- **Factory level** is bought when no machine of the mix can be built (no free slot, or every missing type locked); otherwise the Press and Assembler never unlock at 4 slots. (S031)
- **Hold-off of consumers** applies to delivery contracts only; a production objective counts output. (S031)
- **Casual click** mines at 100 % of the cap until the first Miner, then 25 %. (S031)
- **Bonus bound** follows FR-004: the gate accepts 1.0 to 2.0 for a bonus offer and 1.0 to 1.5 for a standing offer. (S031)

## Gate mechanism
- `GateResult.ContentTuned` gates are measured and reported as transitional while `ContentRevision < TraceGates.AssertsFromRevision` (3), and assert from revision 3. A test pins that only four ids can be transitional. They read the validator with the revision replaced by 3 (`StaticRules.ReadAsRevision3`), so they report what content must fix; the string replace fails closed. (S031)
- Always asserted: conservation, sale neutrality (a partial `Sell` may lose 1 unit to the single floor), partition invariance, remove-rebuild, eco-3, start affordability, 4.0x IP, the engine half of the ratio bounds. (S031)
- At revision 3 all four transitional gates assert and pass; see [content revision 3](./f005-batch-c-content-revision-3.md). (S032)

## BalanceFull self-gating
- The runner counts a skipped test as a failure, so `[Explicit]` is no option. The class passes at once unless the command line selects it (`-testCategory BalanceFull`, or a `-testFilter` naming the class) or `IDLEFACTORY_BALANCE_FULL=1`. `scripts/test-unity.ps1 -Suite EditMode -Category BalanceFull` plays the matrix; the default run does not. (S031)
- Matrix cost is about 75 s (r2) to 116 s (r3) of matrix time against the 20 min budget. `IDLEFACTORY_WRITE_STRATEGY_BASELINE=1` rewrites `strategy-baseline-r3.md`; `IDLEFACTORY_REGENERATE_BALANCE_TRACE=1` rewrites `trace-r3.csv`. (S031, S032)

## Findings and limits
- Casual pin is 78 s (first integer that satisfies the 0.4 rule on policy r2) and is never recalibrated. (S031)
- Contracts-only never reaches run 1 (auto-sell off, the store fills with Ore); this counts as SC-002 evidence and no bootstrap exception exists. (S031)
- `industry.warehouse` is never valued by `T_fluid`, and `T_fluid` overstates revision 3 by 76 % because Research and Items are not modelled, so it only ranks Industry purchases. (S031, S032)
- The cooldown artefact (#466) is gone on r2: the policy takes another available offer instead of waiting. (S031)
