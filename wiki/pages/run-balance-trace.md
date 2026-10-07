---
title: Run balance trace
type: component
sources: [S018]
updated: 2026-10-07
---

# Run balance trace

The content numbers are a versioned hypothesis. A deterministic `BalanceTraceRunner` in the Edit Mode tests drives the real commands through a written reference policy. It records states at 10/15/30/60/90/120 minutes plus a second run, and keeps a golden CSV that is regenerated only on opt-in. The 2-hour and second-run claims need that trace, a named human playtest and owner approval of the `contentRevision`. (S018)

## Trace r1 (T107)

The first trace on content revision 1 misses every pacing band: first Gear at 1,240 s (band 600–900 s), and the $10M prestige threshold is not reached in six simulated hours (Factory Value about $306k at minute 120), so the second run cannot be measured. A Miner-only probe reaches $10M in 56 minutes, so the content rewards the dominant strategy, and offline income is zero because machines stall on full storage. The bands are reported, not asserted; T125 retunes. A scaled scenario (threshold ÷ 100, no pacing claim) proves the SC-001 chain and the non-increasing runs 1–5 trend. Before retuning, T125 must rule out that the reference policy, which never buys storage level 2 before minute 120, causes part of the miss. (S018)

Related: [run economy and prestige](./run-economy-and-prestige.md).
