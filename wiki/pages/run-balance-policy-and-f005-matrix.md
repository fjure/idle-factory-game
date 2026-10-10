---
title: Run balance policy and F005 matrix
type: reference
sources: [S022, S023, S024, S025]
updated: 2026-10-10
---

# Run balance policy and F005 matrix

## F003 trace sources

- Policy r1 (`balance-policy.md`): the policy looks every 20 s, is idle for the last 60 s of each 300 s, issues at most 10 commands per look, follows a no-perfect-timing rule, and builds a fixed machine order. Sensitivity variants run to a $250,000 Factory Value milestone; `probe.ore-only` has no bound. (S025)
- `balance-trace-r2.md` sensitivity: `var.price-80` +699 per mille (integer rounding), `var.contracts-off` +274 and `var.returns` +1123 miss; `var.absence-60m` passes vacuously. (S023)
- The T127 approval (`content-approval.md`) defers all seven trace items to F005 (non-rounding price variant, contract redesign, later-run bands, Ore-only dominance, contract ratios, noise, fit to policy r1). (S024)

## Planned (F005)

Policy r2 (income as net-worth growth, storage on `StorageFull`, greedy Industry by marginal gain per IP, `Sell` and auto-deliver) and nine strategies run runs 1-5 over five look offsets (`trace-r3.csv`). Full matrix is `[Category("BalanceFull")]`, at most 20 min; a fast gate stays in the default run. The non-rounding price variant replaces `var.price-80`. See [F005 plan overview](./f005-plan-overview.md). (S022)

Related: [run balance trace](./run-balance-trace.md), [run economy and prestige](./run-economy-and-prestige.md).
