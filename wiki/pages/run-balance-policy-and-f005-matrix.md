---
title: Run balance policy and F005 matrix
type: reference
sources: [S022, S023, S024, S025, S026, S028]
updated: 2026-10-14
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

## Strategy framework and r2 baseline (T311, merged)

`IStrategyPolicy` plus an ensemble runner play runs 1-5 over 5 look-grid offsets and take the median per run index; `ReferencePolicy` stays byte-identical to the golden. Strategies: chain, ore-only, contracts-only, idle-heavy, casual. The casual look interval is pinned provisionally at L = 73 s (at most 0.4x of chain Factory Value at minute 15); T341 owns the final pin. Mean 427 ms per run-play; the 9-strategy matrix projects to 1.6-2.6 min. `reviews/strategy-baseline-r2.md` is orientation only, with no asserted bands. (S022)

Play-Tester batch-1 findings (`reviews/playtest-agent-batch-1.md`, a proposal; the owner keeps the veto): F1 chain run 1 eligibility 6,548 s is below the ECO-2 band after the adapter; F2 ore-only reaches eligibility before the chain in every run index (0.92x to 0.54x), a possible single-lever gate failure; F3 contracts-only stalls by construction with auto-sell off; F4 casual run 2 is slower than run 1; F5 idle-heavy takes 1.7x to 2.1x the chain. T320, T341 and T345 are the follow-ups. (S022)

See [F005 content schema v3](./f005-content-v3-transitional.md).

Play-Tester batch-A findings (`reviews/playtest-agent-batch-a.md`, a proposal, not a decision): policy r1 uses none of the batch-A verbs; since batch 1 chain run 1 eligibility is 6,528 s (still 72 s under the ECO-2 floor), and ore-only still beats the chain. Standing rewards of 1.67x-1.75x exceed the 1.5x cap in revision 2. See [batch A open decisions](./f005-batch-a-open-decisions.md). (S028)
