# Balance trace r1: content revision 1 under the reference policy

**Task**: T107 (GitHub Issue #189). **Content**: `Assets/IdleFactory/Content/Iron/iron-factory-content.json`, `contentRevision` 1, unchanged.
**Policy and bands**: `balance-policy.md`. **Golden CSV**: `Assets/IdleFactory/Tests/EditMode/Fixtures/Balance/trace-r1.csv` (regenerate with `IDLEFACTORY_REGENERATE_BALANCE_TRACE=1`; a normal run regenerates in memory and compares byte for byte).
**Gate**: `Assets/IdleFactory/Tests/EditMode/Balance/BalanceTraceTests.cs` (14 tests, about 8 s in the Edit Mode run).
**Status**: a **target** comparison of a hypothesis. No band is asserted by the gate; the failures below are the evidence T125 works from. Nothing here is a claim about current performance (SC-003, SC-004 stay targets until T127).

## 1. Verdict per band

| Band | Measured | Expected | Verdict |
|---|---|---|---|
| first Gear (run 1) | 1240 s | 600 to 900 s | FAIL |
| eligibility (run 1) | not reached | 6600 to 7800 s | FAIL |
| second run to eligibility (run 2) | not reached | 4200 to 4800 s | FAIL |
| runs 1 to 5 (ECO-3) | run 1 not reached | non-increasing, each at least 25 % of run 1 | FAIL |
| scaled runs 1 to 5 (ECO-3, threshold scaled) | 2820 / 2800 / 2740 / 2740 / 2600 s | non-increasing, each at least 25 % of run 1 | PASS |

- **Eligibility is not reached**: after 21,600 s (six hours) of play the Factory Value of the reference policy is below $10,000,000. It passes $250,000 at 6040 s (about 100 minutes) and has $306,189 at 120 minutes. The content author's fluid model (`content-record-r1.md` section 7) put the first $10M at about 61 minutes; the real game, played by a policy that follows the Ore, Bar, Plate, Gear chain, is far slower. The fluid model sold every surplus, which the game does not allow (section 4).
- **First Gear** at 1240 s misses the band by 340 s: the Press and the Assembler wait for factory levels 2 and 3 ($800 and $3,000 plus their own cost), and the policy saves for them against an income of a few dollars per second at minute 10.

## 2. Baseline rows (run 1, real catalog)

| Minute | Cash | Factory Value | IP preview | Machines (type:count@max level) | Storage / Factory level | Contracts |
|---|---|---|---|---|---|---|
| 10 | 69 | 7,701 | 0 | miner 3@22, press 1@1, smelter 2@1 | 1 / 2 | 2 |
| 15 | 433 | 18,085 | 0 | miner 3@30, press 1@1, smelter 2@1 | 1 / 2 | 3 |
| 30 | 2,111 | 54,657 | 0 | assembler 1@1, miner 3@37, press 1@1, smelter 3@1 | 1 / 3 | 6 |
| 60 | 5,350 | 135,637 | 1 | assembler 1@1, miner 3@44, press 1@1, smelter 3@4 | 1 / 3 | 8 |
| 90 | 1,974 | 219,609 | 1 | assembler 1@1, miner 3@48, press 1@5, smelter 3@8 | 1 / 3 | 14 |
| 120 | 7,566 | 306,189 | 1 | assembler 1@1, miner 3@53, press 2@5, smelter 4@8 | 1 / 4 | 20 |

The full rows (stock, Items, Research levels, `firstGearAtSec`, `eligibleAtSec`) are in the golden CSV. The Miner levels rise fastest of all machines (see the probe below for a Miner-only policy).

## 3. Sensitivity (ECO-4, FR-025)

Compared at a Factory Value of $250,000. Baseline play time 6040 s.

| Variant | Measured | Bound | Verdict |
|---|---|---|---|
| var.price-80 | 6380 s play (+56 per mille) | within +/- 250 per mille | PASS |
| var.price-120 | 3340 s play (-448 per mille) | within +/- 250 per mille | FAIL |
| var.contracts-off | 6440 s play (+66 per mille) | -50 to +250 per mille | PASS |
| var.contracts-slow | 6200 s play (+26 per mille) | -50 to +250 per mille | PASS |
| var.overdrive-off | 6100 s play (+9 per mille) | 0 to +150 per mille | PASS |
| var.absence-30m | 3700 s play (-388 per mille) | at least 500 per mille | PASS |
| var.absence-60m | 5400 s play (-106 per mille) | at least 500 per mille | PASS |
| var.returns | 9440 s wall (+562 per mille) | 600 to 1600 per mille | PASS |
| probe.ore-only | milestone 1700 s, eligibility 3360 s | informational | probe, no bound |

- **Prices are asymmetric**: a 20 % lower price slows the run by 5.6 %, a 20 % higher price speeds it by 44.8 %. The measurement does not separate the causes; the policy's purchase order also changes with the prices.
- **Offline (FR-025)**: an eight-hour absence removes 38.8 % (minute 30) and 10.6 % (minute 60) of the play time to the milestone, inside the bound of 50 %. The offline return sold nothing (`offline_sales_income` 0): the policy keeps every resource a machine consumes, so the factory stalls on full storage within minutes of leaving, and the credited eight hours at 4/5 effectiveness produce 10,226 and 9,444 completions that mostly find the store full (3,302 and 3,125 partial batches). This is not a change to offline policy revision 1.
- **Repeated short returns** stretch the wall time by 56 % for 67 % of the time away, inside the bound.
- **Probe**: Miners only, Ore sold, no chain. It reaches the $250,000 milestone at 1700 s (the chain policy needs 6040 s) and the $10M threshold at 3360 s (56 minutes; the chain policy has not reached it after six hours), and it never makes a Gear. The content rewards the dominant strategy that `content-record-r1.md` section 2 predicts (12 Miners at level 100). This is the main finding for T125 (ECO-9): the Gear chain is not the faster way to the threshold, so the SC-003 and SC-004 targets cannot be met by tuning prices alone.

## 4. Why the chain is slow (mechanics the fluid model did not have)

- A machine progresses only while its inputs are in stock, so a resource that a built machine consumes cannot be auto-sold; the policy keeps Ore, Bars and Plates and sells only Gears. Surplus is sold by switching auto-sell on and off (940 times in run 1), which sells the whole stock and lets the consumers starve for a moment.
- Storage stays at level 1 through minute 120 in the baseline rows (the policy saves for factory levels first), and Miner batches grow with level, so 29.2 % of the sampled Miner completions are partial (section 6).

## 5. SC-001 chain, ECO-3, ECO-5, ECO-6 (scaled scenario)

The real catalog does not reach the threshold, so the second run, the projection and the SC-001 chain are measured on the **scaled scenario** (prestige threshold $100,000, same game, same commands; see `balance-policy.md` section 6). It is evidence of the mechanics, not of pacing.

- **SC-001 chain** (asserted by `Sc001Chain...`): Gear produced at 1240 s, Ore, Bar, Plate and Gear all present; first Contract delivery at 300 s (timed Bar, which also grants the Tier I Item); Gear Contract delivered at 2400 s; Research purchased at 380 s; prestige at 2820 s awarding 10 IP; the first Industry upgrade (8 IP) bought right after; run 2 reaches eligibility at 2800 s, faster than run 1 (2820 s). The runner recorded **0 violations**: every command changed exactly the Cash, RP, IP, stock and Items it was supposed to change, and the prestige reset exactly the reset list and kept every Item.
- **Second run block and projection (ECO-3)**, scaled: run times 2820, 2800, 2740, 2740, 2600 s, non-increasing, all far above 25 % of run 1. The gain of the capped Industry (x1.03 speed per level, at most 10 levels) is about 7.8 % over five runs: bounded, as the content record states.
- **Item accrual (ECO-6)**: real run 1 (six hours, 98 Contracts) grants Tier I x4, II x3, III x3, the bound of 10 Items per run. Scaled runs grant 2 (run 1), 4 (run 2) and 2 Items (runs 3 and 4); Items survive the prestige, and the policy activates every one when a target and the conflict key allow.
- **Reward to market (ECO-5)**: the mean Cash reward over the market value of the delivered units is 1.49 in real run 1 (1.00 to 2.00 allowed), 1.64 in scaled runs 1 and 2. **RP per completion**: 4.6 in real run 1 (454 RP over 98 Contracts), 3.7 and 2.9 in scaled runs 1 and 2; RP never grows with re-acceptance (flat curves).

## 6. Partial batches (OD-6, OD-9)

Sampled at every look for every machine whose last completion is known (not every completion):

| Run | Samples | Partial | Share | Mean fill of a partial batch | Limit |
|---|---|---|---|---|---|
| real run 1 (6 h) | 10,732 | 3,136 | 29.2 % | 68.6 % of the batch | 100 % the store, 100 % Miners |
| scaled run 1 | 973 | 319 | 32.8 % | 26.9 % | store, Miners |
| scaled run 2 | 891 | 311 | 34.9 % | 25.1 % | store, Miners |

Only Miners complete partially, always because the store is full (`k_cap`), never for lack of input: their batch grows with level (OD-6) and outgrows storage levels 1 to 2 long before the policy can afford level 3. A partial Miner batch delivers on average about a quarter to two thirds of its batch. This is the case that `content-record-r1.md` section 3 predicted.

## 7. Evidence map

- `BalanceTraceTests.cs`: golden byte comparison, an independent second baseline, the contract columns and the six checkpoints, the second-run block and the projection, the SC-001 chain, ECO-3, ECO-5, ECO-6, partial-batch statistics, the sensitivity variants, the policy document and this record (the record must quote the measured first Gear, eligibility and second-run times and name every band).
- Commands and results: see the report of T107; the Edit Mode suite and the settings-noise check ran on the committed result.

## 8. Handoff to T125

1. The first $10M is not reached in six hours by the chain policy, and a Miner-only policy reaches it in 56 minutes: the cost curve (x1.15) against the speed curve and milestones (x1.08, x2 x2 x3 x5) makes Ore the best investment at every level. The levers named in `content-record-r1.md` section 7 (Miner base cost and growth, Industry and Research values, factory and storage costs) are all content values; the spec-fixed numbers (FR-002, FR-007, FR-010 growth and milestones, FR-012) stay.
2. The first Gear waits for factory levels 2 and 3 ($800 and $3,000); the first Gear band depends on the early factory costs and the start Cash.
3. A price change of 20 % moves the time by 6 % down and 45 % up: the economy has a steep response to prices.
4. The offline return is nearly worthless for a factory that keeps its inputs (no sales while stalled); a decision for the owner, not a silent change of offline policy revision 1.
