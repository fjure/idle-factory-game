# Balance trace r2: content revision 2 under the reference policy

**Task**: T125 (GitHub Issue #207), Game Systems/Economy Designer; the Product lens applied to the proposal, the independent verdicts follow in T132. The owner gate is T127, the human playtest T126: **this record approves nothing**.
**Content**: `Assets/IdleFactory/Content/Iron/iron-factory-content.json`, `contentRevision` 2 (record: `content-record-r2.md`). Revision 1 and its trace stay as history (`balance-trace-r1.md`, `Fixtures/Balance/trace-r1.csv`, unchanged, pinned by hash in the gate).
**Policy and bands**: `balance-policy.md`, **unchanged** (policy r1). Every number below is "the reference policy r1 plays content r2"; section 8 lists two artifacts of that policy that a corrected policy would change.
**Golden CSV**: `Assets/IdleFactory/Tests/EditMode/Fixtures/Balance/trace-r2.csv` (regenerate with `IDLEFACTORY_REGENERATE_BALANCE_TRACE=1`; a normal run regenerates in memory and compares byte for byte).
**Gate**: `Assets/IdleFactory/Tests/EditMode/Balance/BalanceTraceTests.cs`. Since T125 it **asserts** the ECO-2 bands, the ECO-3 run times and the scaled chain for revision 2, and the sensitivity bounds except the three OWNER DECISION deviations of section 6.
**Status**: a *proposal*. SC-003 and SC-004 stay targets until the human playtest and the owner approval; the trace is a deterministic simulation of one scripted player and is not a claim about real players.

## 1. Verdict per band (revision 1 against revision 2)

| Band | Expected | r1 | r2 | Verdict |
|---|---|---|---|---|
| first Gear (run 1) | 600 to 900 s | 1240 s | 720 s | PASS |
| eligibility (run 1) | 6600 to 7800 s | not reached | 6940 s | PASS |
| second run to eligibility (run 2) | 4200 to 4800 s | not reached | 4640 s | PASS |
| runs 1 to 5 (ECO-3) | non-increasing, each at least 25 % of run 1 | run 1 not reached | 6940 / 4640 / 4640 / 4320 / 4320 s | PASS |
| scaled runs 1 to 5 (ECO-3, threshold scaled) | non-increasing, each at least 25 % of run 1 | 2820 / 2800 / 2740 / 2740 / 2600 s | 1720 / 1000 / 1000 / 780 / 780 s | PASS |

- First Gear at 720 s is 12 minutes; the first prestige at 6940 s is 115.7 minutes and the second run at 4640 s is 77.3 minutes. The SC-003 target is "near 120 minutes": 115.7 is inside the band (110 to 130 minutes) and 4 minutes early. The SC-004 target is 70 to 80 minutes: 77.3.
- The scaled scenario (threshold divided by 100, no pacing claim) is no longer needed for the second run, but still proves the chain; its second run reaches the scaled threshold at 1000 s.
- The three ECO-2 bands are met by a **point sample**: section 6 shows that single-value changes of 4 to 20 % move the first prestige by up to 6 % and the second run by up to 9 %, which is as wide as the bands themselves.

## 2. Baseline rows (run 1, real catalog)

| Minute | Cash | Factory Value | IP preview | Machines (type:count@max level) | Storage / Factory level | Contracts |
|---|---|---|---|---|---|---|
| 10 | 11 | 4,718 | 0 | miner 3@11, press 1@1, smelter 2@15 | 1 / 2 | 2 |
| 15 | 53 | 13,034 | 0 | assembler 1@1, miner 3@16, press 1@7, smelter 3@18 | 1 / 3 | 3 |
| 30 | 98 | 109,261 | 1 | assembler 1@17, miner 3@33, press 1@25, smelter 3@34 | 2 / 3 | 9 |
| 60 | 352,607 | 1,120,003 | 3 | assembler 2@38, miner 3@44, press 3@41, smelter 4@41 | 4 / 5 | 69 |
| 90 | 30,220 | 4,287,402 | 6 | assembler 2@43, miner 3@60, press 3@45, smelter 4@61 | 5 / 5 | 141 |
| 115.66 (eligible) | 45,722 | 10,019,139 | 10 | assembler 2@56, miner 3@63, press 3@61, smelter 4@61 | 5 / 5 | 203 |
| 120 | 88,389 | 11,518,460 | 10 | assembler 2@57, miner 3@64, press 3@62, smelter 4@61 | 5 / 5 | 213 |

Revision 1 at the same checkpoints (`balance-trace-r1.md` section 2): Factory Value 7,701 / 18,085 / 54,657 / 135,637 / 219,609 / 306,189 at minutes 10 / 15 / 30 / 60 / 90 / 120, with one Assembler at level 1 throughout and storage level 1 until minute 120. The pacing in short:

| Milestone | Target | r1 | r2 |
|---|---|---|---|
| first Gear | 10 to 15 minutes (600 to 900 s) | 20.7 min | 12.0 min |
| 10 minutes | state reported (no numeric target) | 3 Miners@22, Press, 2 Smelters, no Assembler, FV 7,701 | 3 Miners@11, Press, 2 Smelters, no Assembler, FV 4,718 |
| 15 minutes | state reported | FV 18,085, no Assembler yet | FV 13,034, first Assembler |
| 30 minutes | state reported | FV 54,657 | FV 109,261, storage 2, factory 3 |
| 60 minutes | state reported | FV 135,637 | FV 1,120,003, storage 4, factory 5 |
| 90 minutes | state reported | FV 219,609 | FV 4,287,402 |
| 120 minutes | state reported (first prestige near 120) | FV 306,189, prestige not reached in 360 min | FV 11,518,460; the prestige was at 115.7 min with FV 10,019,139 |
| second run | 70 to 80 minutes | not reached | 77.3 min |

The state rows have no target in SC-003 and are reported, not judged. A reading for the Product review: the first ten minutes are *slower* in r2 than in r1 on Factory Value (4,718 against 7,701) because r1's Miner levels were cheap; what changed is the second quarter hour, where r2 buys the Assembler and r1 waited at $3,000.

**Ore-only probe** (`probe.ore-only`: Miners only, Ore sold, no chain): milestone ($250,000) at 2600 s and the $10M threshold at 6020 s, against 3360 s in revision 1. The chain policy now reaches the threshold at 6940 s: the chain is **still 15 % slower than Ore only** (Ore only is 13 % earlier; r1: the chain never arrived). Dominance is reduced, not removed; this is the open ECO-9 finding for the owner (section 6, item 4).

## 3. What changed and the evidence

The method: start from r1 and the findings of `balance-trace-r1.md` section 8, search the content space with the real runner (a scratch harness outside the repository that compiles the repository's trace sources unchanged), then, for the shipped revision, **reverse each change alone** and replay the trace. A row below is the shipped r2 with that one value (or group) put back to its r1 value; "gear / eligible / second run" are in seconds.

| Reversal against shipped r2 | first Gear | eligible | second run | runs 1 to 5 |
|---|---|---|---|---|
| none (shipped r2) | 720 | 6940 | 4640 | 6940 / 4640 / 4640 / 4320 / 4320 |
| start Cash 37 -> 25 | not reached | not reached | not reached | run 1 not reached |
| Miner base 30 -> 10 | 980 | 11540 | 6920 | 11540 / 6920 / 6920 / 5900 / 5900 |
| Smelter base 9 -> 40 | 720 | 14720 | 9220 | 14720 / 9220 / 9220 / 9280 / 9280 |
| Press base 21 -> 150 | 920 | 11260 | 8420 | 11260 / 8420 / 8420 / 9200 / 9200 |
| Assembler base 78 -> 600 | 720 | not reached | not reached | run 1 not reached |
| all four machine bases -> r1 | 5880 | not reached | not reached | run 1 not reached |
| Factory level 2 cost 320 -> 800 | 740 | 6760 | 4500 | 6760 / 4500 / 4500 / 4660 / 4660 |
| Factory level 3 cost 1,900 -> 3,000 | 1040 | 6420 | 4600 | 6420 / 4600 / 4600 / 4540 / 4540 |
| Factory level 4 cost 8,500 -> 12,000 | 720 | 6820 | 4660 | 6820 / 4660 / 4660 / 4320 / 4320 |
| all three factory costs -> r1 | 1060 | 6480 | 4820 | 6480 / 4820 / 4820 / 4600 / 4600 |
| Gear contract cash 4,000 -> 3,840 | 720 | 6620 | 4700 | 6620 / 4700 / 4700 / 4340 / 4340 |
| Plate contract cash 1,400 -> 1,280 | 720 | 6260 | 4740 | 6260 / 4740 / 4740 / 4600 / 4600 |
| both contract cash values -> r1 | 720 | 6720 | 4540 | 6720 / 4540 / 4540 / 4600 / 4600 |
| Calibration -> r1 (x1.03 x 10 levels, 8 IP) | 720 | 6940 | 6340 | 6940 / 6340 / 6340 / 4920 / 4820 |
| Market Contacts -> r1 (x1.02 x 10 levels, 10 IP) | 720 | 6940 | 5020 | 6940 / 5020 / 5020 / 10580 / 10580 |
| Toolmaker -> r1 (x0.98 x 5 levels, 12 IP) | 720 | 6940 | 4640 | 6940 / 4640 / 4640 / 4820 / 4820 |
| Seed Capital -> r1 ($500, 5 IP) | 720 | 6940 | 4640 | 6940 / 4640 / 4320 / 4860 / 5140 |
| all Industry -> r1 | 720 | 6940 | 6480 | 6940 / 6480 / 6180 / 6180 / 7360 |

Findings, in the order of the pacing targets:

1. **First Gear (720 s)**. The Assembler base ($600 in r1) and factory levels 2 and 3 ($800 + $3,000) put the Gear behind about 20 minutes of saving. Putting the three factory costs back moves the Gear from 720 s to 1060 s. Putting the Assembler price back leaves the Gear at 720 s (a Cash burst pays $600 at that look), but the first prestige is then not reached.
2. **First prestige (6940 s)**. The chain machines were priced for a player who sells Ore. Each of the four base costs is individually critical (putting any one back moves the first prestige by +62 % to "not reached"), and together they carry the revision: with all four at their r1 values the first Gear moves to 5880 s. The Miner is the one value *raised*, to $30: with $10 the Ore economy is the dominant strategy (r1 probe 3360 s) and the chain loses 4,600 s (11540 s against 6940 s).
3. **Start Cash is pinned to the Miner base**. $25 with a $30 Miner leaves the first run without a machine ("not reached"); the gate now asserts `startingCash >= Miner base cost`.
4. **Second run (4640 s)**. In r1 the first IP award bought +3 % speed (`industry.calibration`, 8 IP): the second run saved less than 1 % (scaled scenario: 2800 s against 2820 s). The only run-scoped effects the policy can use in run 2 are Calibration, Market Contacts and Toolmaker (Seed Capital and Warehouse apply from the *next* prestige on). Calibration is worth 1,700 s of the second run (6340 s with the r1 values) and Market Contacts 380 s. r2 therefore moves the front of the curve forward: x1.22 per level, three levels (1 + 2 + 3 IP), and x1.19 per level, two levels (1 + 2 IP): the whole 10 IP award (9 spent) buys the full speed and price caps of the first prestige.
5. **Runs 3 to 5 (4640 / 4320 / 4320 s)**. The ECO-3 caps (Industry speed at most x2, price at most x2, cost at least x0.5) are used up by the first prestige, so runs 3 to 5 improve only by the single Toolmaker level and the starting Cash: **the draft's later-progression bands (45 to 55 and 30 to 40 minutes) are not met** (run 3: 77 minutes, run 4: 72 minutes). That is a consequence of the cap, not of the values: reaching them needs a higher cap (an ECO-3 change), see section 6, item 3.

Contract rewards and factory costs are second-order: individually they move the first prestige by 3 to 10 % in either direction (the trace is chaotic at that scale, section 6).

## 4. Sensitivity (ECO-4, FR-025)

Compared at a Factory Value of $250,000. Baseline play time 2260 s (r1: 6040 s).

| Variant | Measured | Bound | Verdict |
|---|---|---|---|
| var.price-80 | 3840 s play (+699 per mille) | within +/- 250 per mille | FAIL (OWNER DECISION, section 6 item 1) |
| var.price-120 | 1980 s play (-124 per mille) | within +/- 250 per mille | PASS |
| var.contracts-off | 2880 s play (+274 per mille) | -50 to +250 per mille | FAIL (OWNER DECISION, item 2) |
| var.contracts-slow | 2820 s play (+247 per mille) | -50 to +250 per mille | PASS (3 per mille from the bound) |
| var.overdrive-off | 2280 s play (+8 per mille) | 0 to +150 per mille | PASS |
| var.absence-30m | 2160 s play (-45 per mille) | at least 500 per mille | PASS |
| var.absence-60m | 2260 s play (0 per mille) | at least 500 per mille | PASS, **vacuous**: the milestone is reached before minute 60, so the absence never starts (r1: 6040 s) |
| var.returns | 4800 s wall (+1123 per mille) | wall time 600 to 1,600 per mille | FAIL (OWNER DECISION, item 2) |
| probe.ore-only | milestone 2600 s, eligibility 6020 s | informational | probe, no bound |

- **Offline (FR-025)**: the eight-hour absence at minute 30 removes 4.5 % of the play time to the milestone (r1: 38.8 %), inside the bound of 50 %. The offline return still sold nothing (`offline_sales_income` 0; 6,661 credited completions, 3,667 of them partial): the policy keeps every resource a machine consumes, the factory stalls on a full store, and the credit is worth only what the store held. This is the r1 finding unchanged; it is not a change to offline policy revision 1.
- **Repeated short returns** (away 10 minutes of every 15): the wall time to the milestone is 4800 s against 2260 s (x2.12; bound x1.6, r1 x1.56). The faster economy loses more to the same absences because nothing is earned while away.
- **Price variants**: `ScalePrices` rounds to whole dollars, so "price -20 %" means Ore $1 (unchanged), Bar $2 (-33 %), Plate $6, Gear $19. At Bar $2 a Smelter turns 2 Ore ($2) into 1 Bar ($2): the smelting margin vanishes, and the policy's fluid model sees no gain from the chain. At the $2.5M milestone the variant is "not reached" in six hours (scratch run); it is a property of the integer FR-012 prices (Ore $1, Bar $3: +50 % for the first step), not of r2: r1 passed this bound only because its milestone came at 6040 s.

## 5. SC-001 chain, ECO-3, ECO-5, ECO-6

- **SC-001 chain** (asserted by `Sc001Chain...`, now on both scaled and real runs): zero violations. In real run 1: first Gear 720 s; 203 Contract deliveries (Gear 67, Plate 68, timed Bar 68; r1: 98 in six hours); 18 Research purchases; the prestige at 6940 s awards 10 IP; the first Industry spend buys all five levels of Calibration and Market Contacts (9 IP); run 2 reaches eligibility at 4640 s, faster than run 1.
- **Runs 1 to 5 (ECO-3)**: 6940 / 4640 / 4640 / 4320 / 4320 s, non-increasing, every run at least 62 % of run 1. Run 3 equals run 2 because the IP of run 2 goes to Seed Capital (10 IP, +$300 from the *next* start) and Seed Capital's effect applies one run late; run 5 equals run 4 for the same reason (Seed Capital level 2, 15 IP).
- **Item accrual (ECO-6)**: real runs 1 to 5 grant Tier I x4, II x3, III x3, the bound of 10; the policy activates every Item.
- **Reward to market (ECO-5)**: the mean Cash reward over the market value of the delivered units is 1.49 in real run 1 (1.00 to 2.00 allowed; r1: 1.49). **RP per completion**: 4.65 in real run 1 (944 RP over 203 Contracts; r1: 4.6); RP stays flat per completion.
- **Partial batches (OD-6, OD-9)**: see `content-record-r2.md` section 3 (Income lost to partial batches). Run 1: 40.8 % of the sampled completions are partial, 706 of 1,454 because of the store (Miners only), the rest because of the inputs; store-limited loss between $54,740 (lower bound, 0.55 % of the run's value) and an unmeasured ceiling; 12.7 % of machine-looks found a machine stopped on a full store.

## 6. Deviations, OWNER DECISION items, and the noise of the trace

**OWNER DECISION** items are not applied and not hidden; each needs the owner (ECO-9, Constitution III).

1. **OWNER DECISION: var.price-80 (+699 per mille, bound +/- 250).** Cause: the integer rounding of the variant (section 4) removes the smelting margin; r2 has no value that reaches the bound without moving a band. Options: (a) accept the variant as a test artifact and replace it by a price modifier that does not round (needs a test change, no production change); (b) change FR-012 Bar/Plate prices (spec change); (c) accept the miss. The gate pins the failure so a fix or a worsening fails the build.
2. **OWNER DECISION: var.contracts-off (+274 per mille, bound -50 to +250) and var.returns (x2.12 wall, bound x1.6).** `contracts-off` misses by 24 per mille: the contracts are a large part of the early income (total contract Cash in run 1 is $567,976, 5.7 % of the run's value, but it arrives early). Putting the Gear and Plate cash back to the r1 ratios (3,840 and 1,280) gives +252 per mille, still 2 above the bound, and breaks ECO-3 (runs 4 and 5 at 4600 s above run 3 at 4540 s); no tested neighbour passes all four bands and this bound together (item 6). `returns` misses because the offline credit is nearly worthless while inputs are kept (the r1 finding, offline policy revision 1 is locked): changing it is an owner decision on the offline policy, not a content value. Neither is applied.
3. **OWNER DECISION: later-progression targets.** The draft's runs 3 and 4 (45 to 55 and 30 to 40 minutes) need the Industry caps raised beyond x2 speed / x2 price / x0.5 cost (canonical test caps) and more IP per run; at the caps run 3 is 77 minutes and runs 4 and 5 are 72 minutes. The Industry front-loading of r2 (all speed and price levels at the first prestige) is also an owner decision: it makes the second prestige feel flat.
4. **OWNER DECISION: Ore-only dominance and the 115.7-minute prestige.** The Ore-only probe still reaches the threshold 13 % earlier than the chain (6020 s against 6940 s), and the first prestige sits 4 minutes before the "near 120" target. Raising the Miner base cost further (to flatten the probe) moves the chain too (Miner 36 gives 6240 s and a second run of 4880 s, outside the band).
5. **OWNER DECISION: contract reward ratios.** r2 raises the Gear and Plate ratios to 1.67 and 1.75 (limit 2.00). A telemetry step up of +20 % is rejected by the validator for Plate and timed Bar (`contract.reward.ratio`); there is no headroom to raise them later.
6. **Noise of the trace.** Single-value neighbours of the shipped revision (the 12 combinations of Gear cash 3,840 / 3,920 / 4,000 and Plate cash 1,280 / 1,330 / 1,360 / 1,400 on the intermediate revision 2 candidate) produced a first prestige between 6,260 s and 6,760 s and a second run between 4,500 s and 5,020 s: the policy's response is chaotic at the scale of the bands (an 8 to 9 % band against a 6 to 9 % scatter). The shipped revision is one of those samples; the playtest, not the trace, decides.

## 7. Values that only live evidence can tune: hypotheses, signals, safe steps

There is no analytics in this task or in the game. "Signal" means what a named human playtest (T126) or a later, separately approved measurement would have to observe; the trace is the only instrument today. The safe step for one revision is at most +/- 20 % of the value. The right-hand columns are the **measured effect of that step in the trace**; "OUT" means the step leaves at least one band (first Gear, eligibility, second run, ECO-3), so it needs owner approval before it is applied (ECO-9). "invalid" means the content validator rejects the step.

| Value | r2 | Hypothesis | Signal | Step -20 % | Step +20 % |
|---|---|---|---|---|---|
| `runStart.startingCash` | 37 | The first Miner is bought in the first seconds and the first upgrade within a minute | time to the second Miner in the playtest; the Miner price is a lower bound ($30) | 30: ok (Gear 760, 6640 / 4620) | 44: OUT (second run 4840) |
| Miner base cost | 30 | Ore-only is no longer the dominant plan | share of players who build more than 6 Miners before a Smelter | 24: OUT (Gear 940) | 36: OUT (first prestige 6240, second run 4880) |
| Smelter base cost | 9 | The Smelter is the first chain purchase, within minutes | time to the first Smelter | 7: OUT (second run 4820) | 11: ok (6720 / 4680) |
| Press base cost | 21 | the first Press by minute 8 | time to the first Press | 17: OUT (second run 8160) | 25: OUT (second run 4840) |
| Assembler base cost | 78 | the first Gear at 10 to 15 minutes | time to the first Gear (the band) | 62: OUT (first prestige 6540, second run 4840) | 94: OUT (first prestige 6520; ECO-3 4560 above 4480) |
| Factory level 2 / 3 / 4 / 5 cost | 320 / 1,900 / 8,500 / 48,000 | each expansion about every 10 to 25 minutes | time between expansions | L2 256 OUT (Gear 920); L3 1,520 OUT (first prestige 6460); L4 6,800 OUT (ECO-3: 4580 above 4520); L5 38,400 OUT (6540) | L2 384 OUT (Gear 920); L3 2,280 ok; L4 10,200 ok; L5 57,600 ok |
| `contract.gear` cash base | 4,000 | Gear contracts are the main early income | Cash from contracts per hour | 3,200: invalid (ratio below 1.0 at some completion) | 4,800: ok (6680 / 4500) |
| `contract.plate` cash base | 1,400 | same | same | 1,120: invalid | 1,680: invalid (ratio above 2.0) |
| `contract.timed-bar` cash base | 510 | the timed contract is accepted about every 4 minutes | acceptance rate | 408: OUT (second run 8300) | 612: invalid |
| Calibration per level | x1.22 | the first prestige feels like a doubling of speed | speed felt by the player in run 2 | 1.176: OUT (ECO-3 4680 above 4600) | 1.264: invalid (`speed.level.dead`) |
| Market Contacts per level | x1.19 | sale price matters at the second prestige | Cash per minute in run 2 | 1.152: OUT (ECO-3: 4640 above 4600) | 1.228: ok (4520) |
| Toolmaker x | 0.639 | one level at 3 IP is bought in run 3 | purchase order of IP | 0.511: ok | 0.767: ok |
| Seed Capital $ | 300 | little used; a placeholder | IP spend share | 240: ok | 360: ok |

For every value the safe adjustment step for one revision is therefore **either no step, or a step on the "ok" side**; any "OUT" step is OWNER DECISION. The table also states what the trace cannot tell: whether real players buy in the order of the script, how long they stay in one session, and whether the 20 % idle windows describe a real player at all.

## 8. Findings about the reference policy (not changed, for the owner)

The T107 note asked T125 to rule out that the policy causes part of the r1 miss. It does, in two places, and it does not explain the whole miss.

1. **Observed income is near zero.** The policy decides whether to *save* for a purchase from the Cash gained per second between looks, but measures it against the Cash *after the previous look's own sales*, so the dump sales and contract rewards of a look never count. At the checkpoints of r1 the observed income is 0.007 $/s at minute 7, 0.0000013 at minute 17, and about 10^-14 at minute 57. The rule `income = max(observed, 1)` then turns every "affordable within 90/120/180 s" wait into "only if the shortfall is below $90/$120/$180": **the policy never saves**; it buys whatever it can afford at each look. A counterfactual with the model income instead (scratch experiment): r1 content reaches the first Gear at 700 s (in band) but still not the threshold; **r2 content on the same corrected policy** reaches the first prestige at 8280 s and the second run at 5020 s (outside the bands). So the artifact explains the r1 first-Gear miss but not the missing $10M, and r2 is tuned to the artifact: a policy r2 needs another content iteration.
2. **The storage trigger rarely fires.** The policy dumps a kept resource at 25 % of the capacity, so the stock stays below the 60 % trigger that buys storage; in r1 one storage upgrade was bought in six hours.
3. **Industry order and the delayed effects.** The policy spends IP in the order Calibration, Seed Capital, Market Contacts, Toolmaker, Warehouse, repeatedly; Seed Capital and Warehouse act one run late. With r2's cheap Calibration and Market Contacts the 4 IP left after Calibration are taken by Seed Capital if it costs 4 IP (second run 4960 s, outside the band; at its r1 price of 5 IP, runs 3 to 5 become 4640 / 4320 / 4320 / 4980 and ECO-3 fails), so r2 prices Seed Capital at 10 IP. Raising its value to $500 makes run 4 slower than run 3 (4860 s against 4640 s): more starting Cash changes the policy's build order for the worse. Seed Capital is therefore a placeholder in r2.
4. **Sensitivity milestone.** The $250,000 milestone suits a slow economy; at r2 it comes before minute 40, so `var.absence-60m` never starts (section 4).

## 9. The W003 workloads and T122

`w003-measurements.md` (T122) measured the 8-hour reconcile on W003-B/S/R, whose catalog is the test-local one of `W003Workload` (Edit Mode) and `W003PlayerFixtures` (player): a `new RunCatalog(1, ...)` built in code with its own speed tables and no reference to `iron-factory-content.json`. A content revision therefore **does not affect W003**, and revision 2 changes no speed bound that those catalogs share. No T122 re-run is needed. What revision 2 *does* change is the shipped-catalog device sets used by the emulator journey (T245): they are regenerated for the shipped catalog (`Fixtures/SaveV2/README.md`, `scripts/android-emulator-journey.ps1`). The shipped worst-case speed bound of the Smelter and Press is x951,625.6 (r1: x704,301.9), 4.8 % below `maxCombinedSpeedPpm`; the 8-hour event bound is `minCycleTicks`-driven and independent of the values.

## 10. Evidence map and handoff

- `BalanceTraceTests.cs`: golden byte comparison, an independent second baseline, the contract columns and checkpoints, the second-run block and projection, the SC-001 chain, ECO-3, ECO-5, ECO-6, partial-batch statistics, the sensitivity variants, the asserted ECO-2 bands for revision 2, the pinned sensitivity deviations, the start-Cash invariant, `trace-r1.csv` history, and this record and `content-record-r2.md` (the records quote the measured numbers and name every band).
- Handoff to T126 (human playtest): record the first Gear time, the time to factory level 3, the Contract acceptance pattern, whether storage is bought before level 4 and the actual first-prestige and second-run times against section 1. Handoff to T127: the owner decisions of section 6 and the policy findings of section 8.
