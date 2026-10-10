---
title: Run balance policy and F005 matrix
type: reference
sources: [S022, S023, S024, S025, S026]
updated: 2026-10-10
---

# Run balance policy and F005 matrix

## F003 trace sources

- Policy r1 (`balance-policy.md`): the policy looks every 20 s, is idle for the last 60 s of each 300 s, issues at most 10 commands per look, follows a no-perfect-timing rule, and builds a fixed machine order. Sensitivity variants run to a $250,000 Factory Value milestone; `probe.ore-only` has no bound. (S025)
- `balance-trace-r2.md` sensitivity: `var.price-80` +699 per mille (integer rounding), `var.contracts-off` +274 and `var.returns` +1123 miss; `var.absence-60m` passes vacuously. (S023)
- The T127 approval (`content-approval.md`) defers all seven trace items to F005 (non-rounding price variant, contract redesign, later-run bands, Ore-only dominance, contract ratios, noise, fit to policy r1). (S024)

## Policy r2 (designed, T301)

`reviews/balance-policy-r2.md` holds the reference policy r2, its strategies and the ensemble. The matrix itself is not run yet. (S026)

- Income is net-worth growth; storage is handled on `StorageFull`; Industry is bought greedily by marginal gain per IP; `Sell` and auto-deliver are policy actions. (S022)
- Nine strategies run runs 1-5 over five look offsets (`trace-r3.csv`). The full matrix is `[Category("BalanceFull")]`, at most 20 min; a fast gate stays in the default run. The non-rounding price variant replaces `var.price-80`. (S022)
- Review fixes: a `manualYield` fluid term, prestige-later at 1.5x and 3x with a cumulative metric C(n), warnings, and the Q2 budget order. QA failed once on the mix units and the missing FR-024 base-price bound; both fixed. Owner answered Q9 yes. (S026)

See [F005 plan overview](./f005-plan-overview.md). (S022)

Related: [run balance trace](./run-balance-trace.md), [run economy and prestige](./run-economy-and-prestige.md).
