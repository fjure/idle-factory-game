---
title: Run balance trace
type: component
sources: [S018]
updated: 2026-10-09
---

# Run balance trace

The content numbers are a versioned hypothesis. A deterministic `BalanceTraceRunner` in the Edit Mode tests drives the real commands through a written reference policy. It records states at 10/15/30/60/90/120 minutes plus a second run, and keeps a golden CSV that is regenerated only on opt-in. The 2-hour and second-run claims need that trace, a named human playtest and owner approval of the `contentRevision`. (S018)

## Trace r1 (T107)

The first trace on content revision 1 misses every pacing band: first Gear at 1,240 s (band 600–900 s), and the $10M prestige threshold is not reached in six simulated hours (Factory Value about $306k at minute 120), so the second run cannot be measured. A Miner-only probe reaches $10M in 56 minutes, so the content rewards the dominant strategy, and offline income is zero because machines stall on full storage. The bands are reported, not asserted; T125 retunes. A scaled scenario (threshold ÷ 100, no pacing claim) proves the SC-001 chain and the non-increasing runs 1–5 trend. Before retuning, T125 must rule out that the reference policy, which never buys storage level 2 before minute 120, causes part of the miss. (S018)

The T121 player journey (#203) uses the same scaled scenario: threshold $100,000 with the IP divisor ÷ 100, because content r1 does not reach $10M in 6 h. SC-001 on unscaled content stays with T125. (S018)

## Trace r2 (T125)

Content revision 2 (`reviews/balance-trace-r2.md`, `content-record-r2.md`, `Fixtures/Balance/trace-r2.csv`) meets the bands under reference policy r1: first Gear 12.0 min, first prestige 115.7 min, second run 77.3 min. Later-run targets are not met. Seven owner decisions are recorded, among them that r2 is tuned to policy r1 and its artifacts (the single-value sensitivity of 4 to 20 % moves the first prestige up to 6 %, as wide as the bands). Sections made with a scratch harness are not reproducible from the commit. The gate now asserts the r2 bands. SC-003 and SC-004 stay targets until the human playtest and owner approval. Because r2 changed the catalog, 87 Play Mode panel tests that hard-coded r1 values failed QA; they now derive expectations from the shipped catalog. (S018)

## Human gates (T126, T127)

The owner played part of the first run on a Windows build of content r2 (about $40,000 Factory Value, no prestige) and was pleased: no conspicuous bugs, good feel. He was about 2.5x behind the scripted policy at minute 15 (about 5,200 against 13,034) and found $1M "very far away", a feel signal for Feature 005. T127 approved revision 2 (blob `66f41fb`) as a fit to policy r1, with the seven trace decisions deferred to Feature 005. Owner decision (spec Session 2026-10-09): the SC-003 and SC-004 minute bands are orientation, not acceptance thresholds; the owner's feel judgment accepts them. The trace keeps reporting the bands and the gate keeps pinning the approved revision. Sources: `reviews/playtest-flo-20261009.md`, `reviews/content-approval.md`. (S018)

Related: [run economy and prestige](./run-economy-and-prestige.md), [player-level test gaps](./player-level-test-gaps.md).
