---
title: Run economy and prestige
type: decision
sources: [S018, S022]
updated: 2026-11-06
---

# Run economy and prestige

This page records the Feature 003 planning decisions for the first Iron loop. The loop is Miner → Smelter → Press → Assembler, followed by contracts, Items, Research, prestige and Industry. (S018)

## Exact numbers

Resource units, Cash, RP, IP and Item counts are `long` values in `[0, 10^18]`. Every add and multiply is checked, and overflow rejects the whole command or advance atomically. The 10^18 bound leaves headroom inside `Int64`, so a sum of two in-range values cannot overflow before the check. Support for values up to 1e150 needs a future explicit numeric, save and content migration. (S018)

The compact display floors to three significant digits with the suffixes K through Qi, so a shown value never implies affordability that does not exist. Inspection shows the exact value. (S018)

## Factory Value and IP

Factory Value is the sum of (S018):

- Cash;
- stock at the effective sale price;
- the Cash actually paid for owned machines and their upgrades;
- invested storage and factory expansion.

The invested amounts are stored, because they are history rather than derivable values. Removal refunds `floor(invested × refundPpm)` with `refundPpm` at most 999,999, so the refund is strictly less than the invested amount. A build-and-remove cycle therefore always lowers Factory Value. (S018)

IP is `ISqrt(floor(c² × FV / divisor))`, which equals `floor(10 × sqrt(FV / 10^7))`. Pinned values: 10 at $10M, 20 at $40M, 30 at $90M, 100 at $1B. The preview is live, and the award uses the values at the confirmation instant. (S018)

The implemented valuation and transition are described in [run prestige transition](./run-prestige-transition.md). (S018)

## Timed and run systems

- **Contracts**: at most one is active and the deadline is inclusive. The scaling index is the number of completions of that offer; expiry does not advance it. Item grants follow deterministic completion-index milestones. An untimed active contract can be abandoned without reward or penalty (OD-4, approved). (S018)
- **Items**: an effect's conflict key is `(effectKind, target)`. A conflicting activation keeps the Item. Targets are machine types or the whole factory (OD-1, approved). (S018)
- **Overdrive**: `cooldownUntil = start + duration + cooldown`. It runs on real time offline and resets on prestige. (S018)
- **Industry**: run-scoped effects apply as soon as they are bought, and the starting-Cash and starting-storage effects apply at the next run. (S018)

## Balance evidence

The balance trace method and its results are recorded in [run balance trace](./run-balance-trace.md). (S018)

Content revision 2 (T125) was the F003 pacing proposal (first prestige about 116 minutes, second run about 77 minutes under the reference policy); it was never owner-approved and is superseded by content revision 3, which the owner approved as shipped on 2026-10-27 (T354, see [content revision 3](./f005-batch-c-content-revision-3.md)). (S022) Panel tests must read numbers from the catalog, not hard-code them. (S018)

## F005 economy changes

Shipped in F005 batches A to C (F005 closed 2026-10-27). The plan below is what was built; mechanics and deviations are in [batch A economy and contracts](./f005-batch-a-economy-and-contracts.md). (S022)

- Prestige threshold: `ceil(base x growthPpm^n / 10^6^n)` with iterated floors, `n = PrestigeCount`, clamped at `maxIndex` (D-K); IP keeps the F003 formula. Starting Cash grows per prestige (D-J). Milestones are fractions of the per-run threshold.
- Industry: per-effect-kind diminishing returns with a precomputed cumulative fold table; no hard caps; practical ceiling at least 4.0x the IP of runs 1-4 (trace gate).
- Build price: iterated-floor growth by owned count and prestige count, early cap return, `base` and `cap` at most 10^12, BigInteger oracle in tests; `Build(..., expectedPrice)` rejects with `build.price.changed`.
- One `SalePricing.Value` (Domain) serves auto-sell, the new `Sell` command and Factory Value, so full-stock sale is FV-neutral.
- Semantic content rules apply from `contentRevision` 3; F003 pacing bands pause until the retune.

See [F005 plan overview](./f005-plan-overview.md). 

Related: [gameplay content v2](./gameplay-content-v2.md), [factory run simulation](./factory-run-simulation.md), [Save V2 and V1 import](./save-v2-and-v1-import.md).

Update (F005 batch A): `SalePricing`, build pricing, the prestige threshold and Industry decay are implemented; see [F005 batch A economy and contracts](./f005-batch-a-economy-and-contracts.md). (S022)
