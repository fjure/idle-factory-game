# Balance trace r3: the content revision 3 tuning record (T345, issue #410, work unit WU56)

**Author**: Game Systems and Economy Designer (MAQA worker, Medium tier at `high` reasoning). **Review lens**: Game Designer/Product Owner (next, on the commit). **Authority**: spec FR-004, FR-016 to FR-025, SC-001, SC-002, SC-004, D-A, D-D, D-K, ECO-4, ECO-6, ECO-9, ECO-12, ECO-13, MIN-1 to MIN-10, `balance-policy-r2.md`, `policy-r2-record.md`, the follow-ups and owner decisions on Issue #410.
**Status**: a tuning proposal and its measurements. **This record approves no content revision** (FR-027): the owner approves `contentRevision` 3 at T354, after T346 and a named human playtest. Every number below is a measurement of the engine on the content named here, by policy r2, and none is a claim about how a human plays.
**Scope of the change**: the two shipped copies of the content (`Assets/IdleFactory/Content/Iron/iron-factory-content.json` and `Assets/IdleFactory/Tests/PlayMode/Fixtures/SaveV1/Resources/RunPlayerFixtures/iron-factory-content.json`, byte-identical), the content validator (the timed-duration cap, the bonus ratio floor, the 100 % mining share cap), the balance gates and bands (the ratio split, the report-only variant, the re-derived bounds), the schema, the `W005-L` fixture, the tests that assumed revision 2 values, this record and the regenerated `strategy-baseline-r3.md` and `trace-r3.csv`. No production domain or simulation formula, no UI, scene, prefab, package, project setting or DevTools code changed. The frozen Save V2 catalog `iron-factory-content-r2.json` is unchanged.

## 1. Objective and order

**Objective.** `contentRevision` 3 passes every revision 3 rule of the validator, and every FR-024 gate asserts and passes on the full matrix of policy r2 (12 definitions, 5 offsets, runs 1 to 5), including the four gates that were transitional at revision 2 (`ratio-content`, the content half of the former `ratio-bounds`, `item-uptime`, `chain-dominance`, `single-lever`). The ECO-2 minute bands (first Gear 600 to 900 s, run 1 eligibility 6,600 to 7,800 s, run 2 4,200 to 4,800 s) are the aim of the pacing but orientation, never a gate (document section 0).

**Order** (#406 review must-do 2): chain dominance first, then the standing ratios, then the ECO-2 bands, re-measuring the whole matrix after each step. The tuning was iterated on a deterministic .NET console harness over the same sources as the Unity tests (`Core`, `Unity/Content`, `Tests/EditMode/Balance`); on the revision 2 content it reproduced `strategy-baseline-r3.md` of T341 to the second (chain 6,468 / 3,928 / 3,652 / 3,652 / 3,648 s, every gate text identical), and the Unity runs of section 12 reproduce its revision 3 numbers. The harness is untracked tooling and not evidence; section 12 is.

1. **Dominance** (`chain.dominance`, `single-lever` run 1): the static rule fails because the value added per slot-second falls along the chain (section 2). Three levers were tried on the matrix: the downstream prices (Bar 5, Plate 16, Gear 40 pushed chain run 1 from 6,468 s to 3,748 s and left Ore-only at 0.88x to 0.98x in runs 2 to 5), the Miner alone (a slower Miner cycle or a steeper Miner upgrade curve fixed Ore-only but left the static rule failing, and with a Miner cost growth of 1.20 at speed growth 1.06, or 1.18 at 1.05, Ore-only was not reached within 6 hours) and the cycle times of the three downstream recipes. The third lever won because it changes no price (so no contract Cash, no Factory Value of stock, no market surface) and no recipe amount, and the output per slot-second then rises along the chain by construction.
2. **Pace**, because shorter downstream cycles make the chain much faster (run 1 fell to about 4,600 s): speed growth 1.08 to 1.06 and upgrade cost growth 1.15 to 1.18 for every machine, which also removes the owner's concern about the Miner out-growing the downstream machines (T316: all four machines have the same growth now). Factory levels 2 and 3 cost more so the first Gear stays in its band.
3. **Ratios** (all three standing offers 1.5, the bonus offer 1.8, the production objective 0.4) and the cooldowns (60 s Gear and timed Bar, 120 s Plate, 300 s production objective).
4. **Industry track** (decay, depth, costs, Seed Capital), the **threshold** (growth 1.10, `maxIndex` 30), the **ladder** (spread over the run, highest index 200), the **build prices** (growth per owned copy and per prestige, capped) and the **milestones**.
5. **Bands and sensitivity**: first Gear and run 2 by the early factory costs and the Industry strength; the `contracts-off` and `returns` bounds re-derived (section 7).

**Not tuned, with the reason.** The four resource prices (Ore 1, Bar 3, Plate 8, Gear 24), the Miner recipe, the recipe amounts, `startingCash` 37, `minCycleTicks`, storage capacities and costs, the research table, Overdrive, the offline policy (D-A), the IP curve (FR-020, fixed $10M reference), the sell steps, the manual mining numbers (section 8), the chain dominance margin (10 %, section 10 answer 2) and the Item effects and durations except the Overclock Core. A change to any of them would not have helped a rule or a gate that failed, and each is visible to the player.

## 2. Closed-form fluid pre-solve

The pre-solve ranks designs before the trace. It is deliberately simple; section 2.4 states what it cannot see.

### 2.1 Output per slot-second (the static rule, exact)

`E(P) = basePrice(P) / S(P)`, `S(P)` the slot-seconds one unit costs over a balanced mix: `S(Ore)` = Miner cycle / 2 Ore, `S(Bar) = cycle(Smelter) + 2 S(Ore)`, `S(Plate) = cycle(Press) + 2 S(Bar)`, `S(Gear) = cycle(Assembler) + 2 S(Plate)`. Every machine has the same speed growth and milestones, so the ratios are the same at level 1 and at every milestone level.

| Resource | Price | Revision 2: slot-seconds | E | Revision 3: slot-seconds | E | E against Ore, revision 3 |
|---|---|---|---|---|---|---|
| Ore | 1 | 1.0 | 1.000 | 1.0 | 1.000 | 1.000 |
| Bar | 3 | 4.0 | 0.750 | 2.8 | 1.071 | 1.071 |
| Plate | 8 | 12.0 | 0.667 | 7.2 | 1.111 | 1.111 |
| Gear | 24 | 29.0 | 0.828 | 16.4 | 1.463 | 1.463 |

Revision 2 fails the rule three times (E falls from Ore to Bar, from Bar to Plate, and the terminal Gear is below Ore). Revision 3 multiplies the three downstream cycles by 0.4 (Smelter 2 s to 0.8 s, Press 4 s to 1.6 s, Assembler 5 s to 2 s): E rises along the chain, and the terminal beats Ore by 46 % against the 10 % margin. The validator reports zero `chain.dominance` diagnostics.

### 2.2 The balanced mix at 12 slots

Machine-seconds per Gear: Miner 4 x 2 = 8, Smelter 4 x cycle, Press 2 x cycle, Assembler 1 x cycle. Revision 2: 8 : 8 : 8 : 5, so 3.3 : 3.3 : 3.3 : 2.1 of 12 slots (3 Miners, 3 Smelters, 4 Presses, 2 Assemblers). Revision 3: 8 : 3.2 : 3.2 : 2, so 5.9 : 2.3 : 2.3 : 1.5 (**6 Miners, 2 Smelters, 2 Presses, 2 Assemblers**; the full table for 4 to 12 slots is in `PolicyR2Tests`). Half of the slots are Miners, which is the intended reading of "Ore is the base of everything".

### 2.3 Closed-form time for the upgrade path

If every machine is raised level by level and all income is invested, then `cost of one level step of all machines at level l = c x g_c^(l-1)` with `c` the sum over the mix of the level-1 upgrade costs (revision 2: 3 x 30 + 3 x 9 + 4 x 21 + 2 x 78 = 357, revision 3: 6 x 30 + 2 x 9 + 2 x 21 + 2 x 78 = 396), income at level `l` is `12 x E(Gear) x s(l)` with `s(l) = g_s^(l-1) x milestones(l)`, and the time is the sum of step cost over income until the cumulative cost reaches the threshold (Factory Value is then almost all invested Cash, as the trace rows show). For Ore-only `c` = 12 x 30 and `E` = 1.

| Model | Revision 2 | Revision 3 |
|---|---|---|
| Closed form, chain | 4,566 s (level 60) | 11,014 s (level 51) |
| Closed form, Ore-only | 3,811 s | 15,246 s |
| Closed form, Ore-only over chain | 0.83 | 1.38 |
| `T_fluid` (policy fluid model, no Industry), chain, index 0 | 6,708 s | 12,584 s |
| `T_fluid`, Ore-only / stop-at-Bar / stop-at-Plate, index 0 | 6,413 / 6,180 / 6,535 s | 18,805 / 15,256 / 14,492 s |
| Trace, chain run 1 (median) | 6,468 s | 7,148 s |
| Trace, Ore-only over chain, run 1 | 0.94 | 2.44 |

Reading: the closed form gets the sign right (Ore-only wins on revision 2, loses on revision 3) and the order of magnitude. The fluid model of the policy (`T_fluid`, no Items, contracts, Research, Overdrive, storage limit or Industry) agrees with the trace on revision 2 within 4 % for the chain (6,708 against 6,468 s) but overstates revision 3 by 76 % (12,584 against 7,148 s). The likely cause, not measured here: half of the revision 3 slots are Miners, and the Research `deep-veins` (x2 Miner output, permanent) and the Diamond Drill Head Item (x2, 60 s) double exactly what the Ore supply limits; neither is in the model. The consequence is bounded: `T_fluid` only ranks Industry purchases by gain per IP (section 4.1 lists the gains), and the trace, not the model, decides every gate. A policy r3 with Research in its fluid model is the proper fix if the ranking ever matters; it is outside this task. It is the reason for the order of section 1: with `E` rising along the chain the closed-form ratio flips, and the trace then has to confirm it with the policy's real behaviour.

### 2.4 What the pre-solve cannot see

Storage limits and the idle windows, the factory levels and builds, Items (up to x2 on the Miner and x1.2 on every machine), Overdrive, Research, the contract reserve that holds consumers off, the 20 s look grid, and the discrete choices of the greedy policy. Examples found by the trace: with the Bar price raised the 1.25 price variant bought a storage level 100 s before the 1.20 variant and finished 100 s later (section 11), and the Plate cooldown moves run 1 by up to 1.7 %. The trace is the proof, not the pre-solve.

## 3. The threshold, IP and 4.0x circularity

Dependencies: the threshold `T(i) = ceil(10,000,000 x g^i)` (index `i` = completed prestiges, clamped at `maxIndex`) fixes the Factory Value at the prestige (about `T(i)`), which fixes the IP award `floor(10 x sqrt(FV / 10,000,000))` (FR-020, fixed), which is spent on Industry levels, which shorten the next run, whose time depends on `T(i+1)`. The 4.0x rule (FR-021, ECO-9) closes the loop from the other side: the practical Industry ceiling (the IP to bring every upgrade to its practical ceiling level) must be at least 4.0 times the highest IP of runs 1 to 4, and SC-004 asks that the IP of runs 1 to 4 is at most 25 % of the ceiling.

The circle is cut in this order: (1) pace on revision 3 (section 1) fixes the run times and the Factory Value at the prestige; (2) `g` is chosen so that ECO-3 (`T(n+1) <= T(n) + 20 s`) holds with the final Industry track; (3) the highest IP over runs 1 to 4 across the chain and both prestige-later strategies is read; (4) the Industry costs and `minGainPpm` are set so that the ceiling is a multiple of that IP and every run's IP buys one to three valuable levels; (5) steps 2 to 4 are repeated on a change.

Threshold growth sweep with the final Industry track (policy r2, 5 offsets; chain medians in seconds, IP of the chain at offset 0):

| `g` | Chain runs 1 to 5 | Chain IP runs 1 to 5 | Highest IP, runs 1 to 4 (any strategy) | Ceiling / highest IP | ECO-3 | `single-lever` |
|---|---|---|---|---|---|---|
| 1.00 | 7,148 / 4,428 / 3,628 / 3,408 / 3,188 | 10 / 10 / 10 / 10 / 10 | 17 | 12.5 | pass | pass |
| 1.05 | 7,148 / 4,488 / 3,728 / 3,508 / 3,308 | 10 / 10 / 10 / 10 / 11 | 18 | 11.8 | pass | pass |
| **1.10** | 7,148 / 4,588 / 3,808 / 3,608 / 3,408 | 10 / 10 / 11 / 11 / 12 | 20 | 10.7 | pass | pass |
| 1.15 | 7,148 / 4,648 / 3,888 / 3,708 / 3,548 | 10 / 10 / 11 / 12 / 13 | 21 | 10.1 | pass | pass |
| 1.20 | 7,148 / 4,728 / 3,968 / 3,808 / 3,628 | 10 / 11 / 12 / 13 / 14 | 22 | 9.7 | pass | pass |
| 1.30 | 7,148 / 4,848 / 4,128 / 4,048 / 3,928 | 10 / 11 / 13 / 15 / 17 | 25 | 8.5 | pass | pass |

The run time is almost insensitive to `g` (the last doubling of Factory Value takes a few minutes), so the threshold growth is a progression and IP lever, not a pacing lever. **Chosen: `g` = 1.10 and `maxIndex` 30** (a threshold of $174M at index 30, the walk is 30 steps; the spec guide asks for about 30). It lifts the IP award by about 5 % per run, keeps the ceiling at 10.7 times the highest IP (the rule needs 4.0), and the chain still gets faster in every run. SC-004: the chain's IP over runs 1 to 4 is 10 + 10 + 11 + 11 = 42, 20 % of the ceiling of 213 IP (limit 25 %); prestige-later-3 reaches 20 IP in a single run.

## 4. The Industry track and the Seed Capital replacement

Per-level values come from the decayed value table (`delta(L+1) = floor(delta(L) x decayPpm / 10^6)`), costs from the curve `ceil(base x growth^(L-1))`, the practical ceiling is the last level whose gain is at least `minGainPpm` (the validator checks it).

| Upgrade | Effect and first value | Decay | Levels | IP cost per level | `minGainPpm` | Practical ceiling | Level gains (ppm) |
|---|---|---|---|---|---|---|---|
| `industry.calibration` | `machineSpeed` all machines, x1.22 | 0.88 | 8 | 1, 2, 3, 5, 8, 12, 18, 27 | 100,000 | 7 | 220,000, 193,600, 170,368, 149,923, 131,932, 116,100, 102,168, 89,907 |
| `industry.market-contacts` | `salePrice` all resources, x1.12 | 0.85 | 5 | 2, 3, 5, 8, 12 | 70,000 | 4 | 120,000, 102,000, 86,700, 73,695, 62,641 |
| `industry.toolmaker` | `upgradeCost` all machines, x0.80 | 0.75 | 3 | 3, 5, 8 | 100,000 | 3 | 200,000, 150,000, 112,500 |
| `industry.seed-capital` | `startingCash` +500 | 0.85 | 10 | 5, 8, 12, 18, 27, 40, 58, 85, 124, 180 | 500,000 (50 % of level 1) | 5 | +500, +425, +361, +306, +260, +221, +187, +158, +134, +113 |
| `industry.warehouse` | `startingStorageLevel` +1 | 1.0 (explicit no decay) | 2 | 20, 40 | 0 | 2 | +1, +1 |

Why these numbers:

- **Depth and cost.** At revision 2 the Industry had 18 levels, six of them valuable (calibration 3, market contacts 2, toolmaker 1), so runs 4 and 5 improved only by Seed Capital (chain run 3 = run 4 = 3,652 s). Revision 3 has diminishing returns instead of hard caps (FR-021, D-C): every run's IP buys the next one to three levels of calibration, market contacts and toolmaker (chain, offset 0, IP spent after runs 1 to 5: 8 IP on three calibration levels and one market contacts level, 11 IP on one level each of calibration, market contacts and toolmaker, 10 IP on market contacts and toolmaker, 13 IP on calibration and the first Seed Capital level, 8 IP on the third toolmaker level). The IP cost curves start at 1 to 3 so the first prestige is not a dead end (`industry.first-affordable.missing` needs a level-1 cost of at most 10).
- **`cost.floor` (0.5x).** The product of the Research discount (tool-discount 0.95^3 = 0.857) and the three decayed Toolmaker levels (0.800 x 0.850 x 0.8875 = 0.6035) is 0.517 of the base upgrade cost, above the 0.5 floor. A fourth Toolmaker level, or a deeper first level, fails `cost.floor`.
- **Sale price and Factory Value (ECO-7).** The largest stock the top storage level holds (16,000 Gears at $24) at the largest sale multiplier must stay at most 10 % of the threshold, otherwise stock dominates Factory Value (the F003 content record fixed this at 9.24 %). With market-contacts at x1.19 for six levels the worst case was 13.6 %. The final track gives **998,230 dollars, 9.98 %** (`RunContentCanonicalTests`, section 11); Research and Industry together leave 0.02 percentage points, so any further raise of Bargaining, Gear Market Analysis or Market Contacts needs a cut elsewhere.
- **Small additive upgrades cannot decay (T331 note).** `startingStorageLevel` +1 cannot decay (the amount floors to 0 after one level), so `industry.warehouse` declares the explicit `decayPpm` 1,000,000 and two levels. `startingCash` +500 survives ten levels (the tenth is +113), so no level is dead.

### 4.1 Seed Capital replacement and the zero-gain check

The placeholder (+300 Cash per level, 10 levels, IP 10 x 1.5^(L-1), no decay) cost 1,134 IP to complete, 90 % of the old ceiling of 1,263 IP, and was worth almost nothing in the fluid model: level 1 shortened the time to the threshold by 100 s for 10 IP (10 s per IP against 2,139 s for the first calibration level), so the policy bought it only when nothing else was left and casual and prestige-later accumulated 18 to 49 IP unspent (#406 report, `industry.seed-capital` zero model gain). The replacement is +500 Cash with decay 0.85, ten levels at 5, 8, 12, 18, 27, 40, 58, 85, 124 and 180 IP (the practical ceiling is level 5; the ten levels keep every revision 2 save loadable, since revision 2 allowed ten, section 9). Model gain per level (`T_fluid`, chain, index 0, seconds of the threshold time removed): 324, 119, 60, 40, 2, 38 for 5, 8, 12, 18, 27, 40 IP, that is 65, 15, 5, 2.2, 0.1, 1.0 seconds per IP, against 2,139 (calibration 1), 1,559 (calibration 2), 1,303 (market 1), 1,419 (toolmaker 1) and 580 (calibration 6) seconds per IP. Seed Capital therefore stays the last thing the policy buys. **Check result:** the full matrix of revision 3 records no affordable level with a non-positive model gain (`industry_zero_gain.*` is empty), so no strategy parks IP behind a blind spot except the stated `industry.warehouse` (capacity has no effect in a model without a storage limit). The chain keeps 0 to 4 IP unspent per run, casual and the prestige-later strategies a few more; `W-later` stays unraised (section 6). Level 5 and 6 of Seed Capital have a model gain of 2 and 38 s (noise of the 20 s step), and the ceiling is the last level that still gives half of level 1 (260 of 500 Cash at level 5, 221 at level 6).

Model gains of the first levels (seconds of `T_fluid` removed, chain, index 0, no other Industry level):

| Upgrade | L1 | L2 | L3 | L4 | L5 | L6 |
|---|---|---|---|---|---|---|
| calibration | 2,139 | 1,559 | 1,171 | 916 | 738 | 580 |
| market-contacts | 1,303 | 946 | 754 | 635 | 494 | - |
| toolmaker | 1,419 | 1,063 | 577 | - | - | - |
| seed-capital | 324 | 119 | 60 | 40 | 2 | 38 |
| warehouse | 0 | 0 | - | - | - | - |

## 5. Revision 3 values, before and after

Every changed value of the two shipped copies. "R2" is the shipped revision 2. The reason column names the rule, gate or finding.

| Path | R2 | R3 | Reason |
|---|---|---|---|
| `contentRevision` | 2 | 3 | The semantic rules and the asserting gates apply from revision 3 (ECO-4). |
| `recipe.smelt-iron-bar` `durationTicks` | 20,000,000 | 8,000,000 | `chain.dominance`: Bar E 0.75 to 1.07 (section 2.1). |
| `recipe.press-iron-plate` `durationTicks` | 40,000,000 | 16,000,000 | Plate E 0.67 to 1.11. |
| `recipe.assemble-gear` `durationTicks` | 50,000,000 | 20,000,000 | Gear E 0.83 to 1.46; terminal beats Ore by 46 %. |
| every machine `speedGrowthPpm` | 1,080,000 | 1,060,000 | Pace after the shorter downstream cycles; same growth for every machine (T316). |
| every machine `cost.growthPpm` | 1,150,000 | 1,180,000 | Pace; the weakest single-lever margin is 1.21x at every run (Ore-only is 1.8x to 2.4x slower). |
| every machine `buildPrice` | owned 1.00, prestige 1.00, cap = base | owned 1.15, prestige 1.04, cap 3 x base | F11: a second and third copy cost more (Miner 30, 35, 40; cap 90). The Miner stays reachable from the start Cash plus the 60 s window at index 30 (`start.reachable`: 37 + 60 = 97 >= 90). MIN-4: the first Miner costs at most the start Cash up to prestige index 5. The pace effect is under 1 %. |
| `factoryLevels` level 2 cost | 320 | 480 | First Gear back into 600 to 900 s (740 s median). |
| `factoryLevels` level 3 cost | 1,900 | 3,000 | Same. |
| `contract.gear` ratio / cooldown | 1.666666 / 0 | 1.5 / 60 s | A-F3, FR-004, SC-001. Cooldown: a 60 s pause makes the offer a rhythm and not a permanent 1.5x channel on one resource (policy r2 takes another available offer while one cools, T363 QA). |
| `contract.plate` ratio / cooldown | 1.75 / 60 s | 1.5 / 120 s | A-F3. T363 QA: revisited with policy r2, section 6 (+1.7 % at 300 s). |
| `contract.timed-bar` ratio / cooldown | 1.7 / 0 | 1.5 / 60 s | A-F3. |
| `contract.bonus-plate` ratio | 1.5 | 1.8 | Owner decision 2026-10-22: about 1.8, above the standing ceiling (rule `contract.ratio.bonus`). |
| `contract.produce-plate` ratio | 0.25 | 0.4 | `contract.ratio.produce`: 0.4 + 1.5 = 1.9 <= 2.0 (T363 QA: zero headroom at 1.75). |
| `contractItemLadder` indices | 0, 1, 1, 3, 4, 5, 6, 9, 10, 10 | 0, 1, 3, 6, 12, 25, 50, 100, 150, 200 | The ten grants were all within the first ten completions of every run. Spread over the run, the highest index 200 covers the 163 completions of one offer that revision 2 measured (#387 (d)). `perRunCap` stays 10 = the number of entries. |
| `item.overclock-core` `durationMs` | 120,000 | 90,000 | `ladder.uptime`: 10 x 90 s = 900 s = 0.5 x 1,800 s (preferred fix, no schema change). |
| `prestige.threshold` growth / `maxIndex` | 1.00 / 10 | 1.10 / 30 | FR-019 (a rising function), section 3. |
| `milestones` | none | 1,000 / 10,000 / 100,000 / 500,000 ppm | 0.1 %, 1 %, 10 % and 50 % of the threshold, which the chain passes at about 14, 31, 57 and 100 minutes of run 1 (FR-018). |
| `industry.calibration` | 3 levels, no decay | 8 levels, decay 0.88, ceiling 7 | Section 4. |
| `industry.market-contacts` | 2 levels x1.19, no decay | 5 levels x1.12, decay 0.85, ceiling 4, base cost 2 | Section 4 (ECO-7 stock bound). |
| `industry.toolmaker` | 1 level x0.639 | 3 levels x0.80, decay 0.75, ceiling 3 | Section 4 (`cost.floor`). |
| `industry.seed-capital` | +300 x 10 levels, base 10, no decay | +500 x 10 levels, base 5 x 1.45, decay 0.85, ceiling 5 | Section 4.1. |
| `industry.warehouse` | 2 levels | unchanged (`decayPpm` 1,000,000 already explicit) | A +1 level cannot decay (T331). |

**Not changed on purpose:** the manual mining block (section 8), `chainDominanceMarginPpm` (section 10), Research costs, Overdrive, storage levels, prices, `startingCash`.

## 6. Results on revision 3 (policy r2, 5 offsets, runs 1 to 5)

Medians of the time to eligibility per run index; every ratio is the strategy median over the chain median of the same run (above 1.00x means slower than the chain). Source: `strategy-baseline-r3.md` section 3 and 5, regenerated for this commit.

| Strategy | Run 1 | Run 2 | Run 3 | Run 4 | Run 5 |
|---|---|---|---|---|---|
| chain (seconds) | 7,148 | 4,588 | 3,808 | 3,608 | 3,408 |
| ore-only | 2.44x | 2.30x | 2.07x | 1.91x | 1.81x |
| stop-at-bar | 1.46x | 1.44x | 1.32x | 1.27x | 1.26x |
| stop-at-plate | 1.26x | 1.26x | 1.23x | 1.21x | 1.24x |
| contracts-only | not reached | not reached | not reached | not reached | not reached |
| market-only-chain | 1.66x | 1.52x | 1.41x | 1.27x | 1.26x |
| idle-heavy | 1.21x | 1.35x | 1.47x | 1.57x | 1.56x |
| casual | 1.68x | 2.26x | 2.60x | 2.77x | 2.95x |
| prestige-later-1.5 | 1.00x | 0.97x | 0.94x | 0.93x | 0.99x |
| prestige-later-3 | 1.00x | 0.90x | 0.93x | 0.88x | 0.91x |
| chain+mining | 0.98x | 0.99x | 0.99x | 0.99x | 1.04x |
| mining-only | not reached | not reached | not reached | not reached | not reached |

Revision 2 for comparison (T341): chain 6,468 / 3,928 / 3,652 / 3,652 / 3,648 s, Ore-only 0.94x to 0.69x, stop-at-Bar 0.69x to 0.80x, stop-at-Plate 1.02x to 1.11x.

**Gates (all assert; none is transitional; no `REPORTED` line is written):**

| Gate | Result | Measured on revision 3 |
|---|---|---|
| `conservation` | pass | 0 violations over 60 histories |
| `sale-neutrality` | pass | 0 out of rule |
| `partition-invariance` | pass | identical state for 2 strategies |
| `remove-rebuild` | pass | 10 machines replaced, never raised |
| `eco-3` | pass | 7,148 / 4,588 / 3,808 / 3,608 / 3,408 s |
| `ratio-bounds` (engine half, always asserted) | pass | effective ratio 1.498 to 1.800 over 19,203 deliveries, none beyond 1.0 to 2.0 |
| `ratio-content` (content half) | pass | 19,028 standing deliveries, highest configured ratio 1.5; 175 bonus deliveries, lowest configured ratio 1.8 |
| `item-uptime` | pass | `ladder.uptime` holds (10 x 90 s = 900 s), most Items granted in one run 10 (bound 10) |
| `start-affordability` | pass | smallest margin 65 Cash |
| `chain-dominance` | pass | holds at revision 3, no diagnostic |
| `single-lever` | pass | none faster; weakest margin stop-at-Plate 1.21x |
| `contracts-only-longer` | pass | run 1 not reached |
| `chain-within-6h` | pass | slowest 7,156 s |
| `ip-4x` | pass | ceiling 213 IP, highest IP 20 (prestige-later-3, run 4), ratio 10.7 |
| `chain-plus-mining` | pass | advantage per run 2.2 / 0.8 / 0.8 / 0.6 / -3.5 % against 10 % allowed |

**ECO-2 bands (orientation, `BalanceTraceReport.Bands()`, policy r2, offset 0):**

| Band | Measured | Expected | Result |
|---|---|---|---|
| first Gear (run 1) | 740 s | 600 to 900 s | inside (revision 2: 660 s) |
| eligibility (run 1) | 7,140 s | 6,600 to 7,800 s | inside (revision 2: 6,460 s, 140 s below) |
| second run to eligibility (run 2) | 4,580 s | 4,200 to 4,800 s | inside (revision 2: 3,920 s, 280 s below) |
| runs 1 to 5 (ECO-3) | 7,140 / 4,580 / 3,720 / 3,600 / 3,400 s | non-increasing, each at least 25 % of run 1 | inside |
| scaled runs 1 to 5 (ECO-3, threshold scaled) | 1,820 / 1,180 / 1,100 / 920 / 920 s | same | inside (revision 2 failed this orientation band) |

**Warnings (never gates):** W-later not raised (C(n) of prestige-later-1.5 and -3 above the chain's: C(5) 24,380 and 28,000 against 22,620 s); W-market not raised (market-only-chain 1.26x to 1.66x: contracts never cost time); W-mining not raised (`mining-only` never reaches a threshold, highest Factory Value read 5,797, it mines 17,280 Ore in run 1); contracts-only not reached (P4c). The contract share of income is 5.7 % in run 1 and falls to 1.9 % in run 5, the mean reward-to-effective-value ratio of a delivery is 1.50.

**Casual.** The look interval stays pinned at 78 s (document 7.1, never recalibrated). On revision 3 the Factory Value at second 900 is 4,190 (casual) against 11,156 (chain), ratio **0.376** (limit 0.400, inside); the search would return 76 s. Casual run 1 is 1.68x the chain.

**Completions (the basis for `perRunCap` and the reachable range of `contract.quantity.capacity`).** Chain run 1 completes 59 Gear, 43 Plate, 21 production and 60 timed Bar offers (184 in all with the bonus offer). The highest count of one offer in any strategy, offset and run is 104 (`contract.timed-bar`, 379 units required), 89 for Gear (319 units); revision 2 measured 163 (719 units). The ladder's highest index 200 (1,061 units at index 200 against the 16,000 top capacity) covers both. Items granted per run: chain 3/3/3, 3/3/2, 3/3/2, 3/2/2, 3/2/2 (tier 1/2/3); the highest of any run is 4/3/3 = 10 = `perRunCap`, so the cap binds only for the late prestige-later runs.

**Report-only variant `contracts-only+sale` (owner decision 2026-10-22).** Contracts-only plus one `Sell` of the whole Ore stock at the first look at which a Miner stands (20 Ore, 20 Cash, one sale per run). The stall is still the SC-002 evidence and no exception is applied to the gate. What the one sale does (medians over the five offsets, `strategy-baseline-r3.md` section 3): the factory is built in full (first Gear at 688 s, 12 machines, all four factory levels, all 18 Research levels, 3 storage levels, 277 upgrades at offset 0), 75 contracts are delivered and the Factory Value reaches 94,240 at 1,800 s and **465,577 at 3,600 s, then stays there until the 6 h limit** (19,180 machine-seconds held off by the contract reserve): the stores fill with unsold Bars, Plates and Gears, no contract can use them (an offer needs 100 to 400 units, the stores hold 16,000), and contract rewards alone (1.5x of a few hundred units per cooldown) cannot pay another upgrade. So the answer to the owner question "is the stall only a bootstrap artefact" is: no. Auto-sell off removes the market half of the economy, and one sale does not restore it (section 13, question 3).

## 7. Sensitivity bounds re-derived (FR-025, tolerances only, D-A)

Measured on revision 3 with policy r2 at offset 0 (the absences of the variants sit on the 20 s look grid, so only offset 0 plays them; the non-absence variants were also played at all five offsets and moved by at most 2 per mille of the baseline). Baseline: 2,380 s of play to the $250,000 Factory Value milestone; the chain's eligibility spread over the five offsets is at most 25 per mille.

| Variant | Measured | Bound | Verdict |
|---|---|---|---|
| `var.price-mod-125` | 2,140 s (-101 per mille) | within +/- 250 per mille | pass |
| `var.price-mod-120` | 2,040 s (-143 per mille) | within +/- 250 per mille | pass |
| `var.contracts-off` | 3,040 s (**+277 per mille**; five offsets 275 to 277) | -50 to **+350** per mille (was +300) | pass |
| `var.contracts-slow` | 2,760 s (+159 per mille) | -50 to +350 per mille | pass |
| `var.overdrive-off` | 2,500 s (+50 per mille) | 0 to +150 per mille | pass |
| `var.absence-30m` | 1,800 s play (-244 per mille) | at least 500 per mille | pass |
| `var.absence-60m` | 2,380 s (0; the milestone comes before minute 60) | at least 500 per mille | pass (vacuous) |
| `var.returns` | 3,900 s wall: **1,520 s added** over the baseline play time, 2,400 s away (63 % of the absence added, 37 % recovered) | **added wall time between 0 and the time away** | pass |
| `probe.ore-only` | milestone 3,800 s, eligibility 17,420 s | informational | pass |

- **`contracts-off`: 300 to 350.** The rule of T341 (round up to the next 50 the measurement plus the chain's offset spread) gives 277 + 25 = 302, so 350. The old +300 also passes (277), but with 23 per mille of room; the next retune would have to move it again. Contracts carry RP (Research) and the Items of the ladder, which is why a player who ignores them loses 28 %; this is the intended "contracts matter" half of SC-005 and not a defect. The spread test (`TheSensitivityTolerancesAreAtLeastAsWideAsTheMeasuredSpreadOfTheOffsets`) asserts every remaining tolerance is at least the measured offset spread.
- **`returns`: from a share of the baseline to added wall time (owner decision 2026-10-22).** The F003 bound (600 to 1,600 per mille of the baseline play time, widened to 2,100 on revision 2) moves with every retune that shortens the run, since the absences stay 600 s every 900 s: it measured 1,920 per mille on revision 2 (the reason T341 widened it from 1,600 to 2,100) and 1,638 per mille on revision 3. The variant is expressed as the wall time to the milestone minus the baseline play time: between 0 (a return never saves wall time: the player who stays can reinvest) and the time spent away before the milestone (a return never costs more than its absence: the factory keeps working at the 80 % offline effectiveness). Measured 1,520 s of 2,400 s away. The bound has no constant and needs no re-derivation after the next retune. It never changes the 80 % effectiveness or the 8-hour cap (D-A).

## 8. Validator rules and the manual mining block

Static results (`RunContentValidator` at revision 3, shipped content): **0 diagnostics**. At revision 2 content the validator read as revision 3 reported 7 (`contract.ratio.standing` x 3, `ladder.uptime` x 1, `chain.dominance` x 3).

New and changed rules (owner decisions, each with tests):

- **Timed members (2026-10-21).** `cooldownMs` and the bonus `windowMs` are at most `MaxTimedDurationMs` (604,800,000 ms, seven days), reported as `contract.duration.out-of-range`, the code of `durationMs`; below the minimum (`cooldownMs` < 0, `windowMs` < 1) stays `value.out-of-range`. It applies at every revision. The schema has the same maximum and `RunContentSchemaConsistencyTests` checks it. Shipped values: cooldowns 60 s, 120 s, 300 s; window 900 s.
- **Bonus ratio floor (2026-10-22).** From revision 3 a bonus delivery offer pays more than 1.5 and at most 2.0 (`contract.ratio.bonus`); revision 2 content keeps validating; a bonus production objective is judged by `contract.ratio.produce`. Shipped: 1.8.
- **Hand mining share (2026-10-15).** `maxShareOfMinerPpm` is at most 1,000,000 (was a structural 3,000,000), so hand mining never beats a Miner. The guide example moved from 1,500,000 to 1,000,000, and the tests that used the larger share as headroom use a smaller yield instead.
- The rule list (`RuleCodes`) gains no code, so `docs/contracts/run-reason-codes.md` (runtime reason codes) is unchanged; the content rule table is in `specs/005-economy-contracts/contracts/gameplay-content-v3.md` and the author guide.

**Manual mining (T358 constraints), unchanged on purpose.** `baseYieldMilli` 100, cap 10 per second, window 60 s, `referenceMinerLevel` 10, `maxShareOfMinerPpm` 250,000, `miningAdvantageMaxPpm` 100,000. At the cap hand mining yields 1.0 Ore/s. The reference Miner (level 10, x3.38) with the maximum Industry speed (x2.97) makes 10.0 Ore/s, so hand mining is 10 % of one Miner against the allowed 25 % (the review asked for the Miner at about 4x the cap yield or more). The dead-level rule has nothing to check (no `manualYield` source exists in the content). MIN-4 holds: the first Miner costs 30 x 1.04^i (37 at index 5), so hand mining is not needed up to prestige index 5, and at index 30 the capped price of 90 needs 53 Cash from the 60 s window. `start.reachable` stays as implemented (mine, sell, mine). The held-click strategies measure it: `chain+mining` is within +2.2 % to -3.5 % of the chain, `mining-only` never reaches a threshold.

## 9. Drift-class review (owner decision 2026-10-21)

Save V3 loads a save of an older content revision through the drift rules of T335: removal of an offer or a changed objective is dropped with `contract.dropped`; the classes "bonus to standing with a leftover record" and "presence of `DurationMs` changed" still load as `checkpoint.invalid` (discardable damage). The revision 3 retune produces none of these classes:

- No offer is removed, added or renamed (the five IDs are unchanged); no objective changes (`produce` stays `produce`, the four `deliver` offers stay `deliver`).
- No class changes: `contract.bonus-plate` stays the only `bonus` offer with its record; the three standing offers stay `standing`.
- `durationMs` is present on `contract.timed-bar` (240,000 ms) before and after, absent on the others before and after.
- Cooldowns only grow (Gear 0 to 60 s, Plate 60 to 120 s, timed Bar 0 to 60 s, production 300 s unchanged) and the window is unchanged (900 s), so no saved `readyAtUtcMs` or `windowEndUtcMs` of revision 2 exceeds the new future-instant bound (departure + cooldown or window + seven days).
- The ladder indices change but the counts are saved per offer and the run-wide total (`runCompletionTotal`) is never lowered, so no Item is re-granted.

**No follow-up is needed.** The Industry track is the one place where a retune can produce a drift class that T335 does not cover: a saved level above the new `maxLevel` fails the load (`PermanentState.TrySetIndustryLevel` returns `LevelMax`, `SaveV2Mapper` requires `Ok`). No `maxLevel` shrinks, so every revision 2 level stays valid (risk 2 in section 13).

## 10. Answers to the comments on Issue #410

1. **#375 finding 9 (placeholders).** Ratios 1.667 / 1.75 / 1.7 to 1.5; the overclock-core Item to 90 s; build prices grow (1.15 per owned copy, 1.04 per prestige, capped at 3x, no longer flat with cap = base); the threshold rises (1.10, `maxIndex` 30); Industry decay is set on every upgrade (10^6 only for the warehouse); the milestones list has four entries.
2. **#379 (T316, `chain.dominance`).** The shipped content now passes the rule, and not with a stretched recipe: only the three downstream cycles changed (x0.4) and every machine has the same speed growth (1.06), so the Miner no longer out-grows the downstream machines. Open decisions: (1) all machines at the same level: kept (the stricter reading); with equal growth and milestones the ratios are level-independent, so the rule gives the same verdict at every milestone level. (2) The 10 % margin was not recalibrated: the static terminal margin is 46 % and the trace shows Ore-only at 1.8x to 2.4x, stop-at-Bar 1.26x to 1.46x and stop-at-Plate 1.21x to 1.26x of the chain time, so the margin is a floor and not what separates the strategies; it stays 10 %. (3) Lead lever: the cycle times of the three downstream recipes, then growth (section 1). (4) The static gate ignores modifiers: the single-lever and chain+mining trace gates are the proof (they pass). A recipe cycle as a reported error (instead of a silent skip) and a rule that rejects content without an input-free recipe are validator questions and are not changed here (section 13, question 4). (5) The 20 s Miner stays; the guide example is not changed.
3. **#396 (T331).** See section 4: warehouse `decayPpm` 10^6 (+1 cannot decay), Seed Capital +500 decays 0.85 to +113 at level 10; `minGainPpm` keeps its two units (ppm of the base for factor kinds, ppm of the level-1 amount for additive kinds), the owner question about one unit is open (section 13, question 5); decay rates and `minGainPpm` values were chosen from the fluid model and the trace and are hypotheses until player data exists; `maxIndex` 30.
4. **#396 QA (schema).** `gameplay-content-v3.schema.json`: `prestige.threshold.maxIndex` maximum 10,000 and the description refreshed (the bound rule is implemented), `decayPpm` / `practicalCeilingLevel` / `minGainPpm` descriptions state the revision 3 range (1 to 10^6), the last-level definition and the two units; `RunContentSchemaConsistencyTests` pins the new maxima.
5. **#385 (T358).** Section 8: no change to yield and cap, with the 10 % result; `maxShareOfMinerPpm` capped at 100 % in the validator (owner decision 2026-10-15); `start.reachable` unchanged; MIN-4 holds to index 5.
6. **Owner decision 2026-10-15.** F3 (rewards <= 1.5): done. F1 (chain faster than Ore-only in every run, measured with policy r2): done, 1.8x to 2.4x. The ECO-2 band stays (run 1 7,140 s, inside). F11 (2nd and 3rd copies not more expensive): a defect of the flat rule, fixed with the T323 build price curve (Miner 30, 35, 40). #387: `perRunCap` stays the total number of ladder entries (10), the capacity bound is the ladder's highest index 200 (>= 163), see section 6. F12 (a human playtest before the tuning): the tuning rests on policy r2 runs only; I could not see a playtest record, T346 and the human gate T353 stay (section 13, question 1). F2/F4: done in T363.
7. **T363 QA (#466).** The Plate cooldown is 120 s (checked with policy r2 at 0 / 60 / 120 / 300 s: run 1 7,100 / 7,160 / 7,140 / 7,220 s); the bonus offer pays 1.8 above the standing 1.5; the production objective has 0.1 of headroom (0.4 + 1.5).
8. **2026-10-21 cap and drift-class review.** Section 8 and 9.
9. **Batch-B follow-up (A-F1, A-F3).** Chain run 1 eligibility 7,148 s (92 s below the floor before, 548 s above it now); Ore-only slower than the chain at every run index; standing rewards 1.5. F-B10: the scripted tinkerer stays far below the chain, a human run is still needed before the content is judged (T353).
10. **#409 QA.** `ContractsPanelTests` adds `PlateRequired - 1` Plates instead of a typed 99 (and `PlateRequired * 3 / 10` instead of a typed 30).
11. **#406 reviews.** Must-do 1: `ratio-bounds` is the always-asserted engine half (configured ratio 1.0 to 2.0, effective 1.0 within one unit up to 2.0) and `ratio-content` the content-tuned half (standing <= 1.5, bonus in (1.5, 2.0]); at revision 3 all four content-tuned gates assert and no `REPORTED` line remains. Must-do 2: order followed (section 1). Must-do 3: 90 s. Must-do 4: section 6. Must-do 5: done. Check/report: bonus 1.8 with the strict "> 1.5" revision 3 rule (the gate stays 1.0 to 2.0); `returns` as added wall time and `contracts-off` re-derived (section 7); casual pin stays 78 s, the ratio is 0.376; `industry.seed-capital` checked (section 4.1); the contracts-only stall is recorded as the SC-002 evidence and a report-only variant with one Ore sale at the start is added (section 6).
12. **#406 owner decisions 2026-10-22.** All three done: bonus about 1.8 and the rule; `contracts-off` and `returns` re-derived after tuning (returns as added wall time); the report-only variant.

## 11. Worst-case figures of revision 3

Recomputed from the loaded catalog by `RunContentCanonicalTests` (the Industry factors are the decayed per-level value tables the engine folds; Research at maximum, the best Item per target and Overdrive).

| Machine | Worst composed speed (ppm) | Batch at `minCycleTicks` | Cycle ticks with the batch |
|---|---|---|---|
| `machine.assembler` | 174967305126 | 8749 | 1,000,073 |
| `machine.miner` | 174967305126 | 8749 | 1,000,073 |
| `machine.press` | 244954227176 | 15310 | 1,000,024 |
| `machine.smelter` | 244954227176 | 30620 | 1,000,024 |

All stay under `maxCombinedSpeedPpm` (10^12). Sale-price effect on Factory Value: the largest stock the top storage level holds (16,000 units) at the largest sale multiplier of each resource is $93,748 (Bar), $249,995 (Plate), $31,249 (Ore) and **998230** dollars for a store full of Gears, 9.98 % of the $10,000,000 threshold (the test asserts at most 10 %). `removalRefundPpm` is 500,000, so a build followed by a removal strictly lowers the Factory Value. The reward-to-market ratio of every offer is 1.5 (standing), 1.8 (bonus) and 0.4 (production) at every completion index 0 to 1,023.

## 12. Runs, fixtures and reproduction

Evidence that is produced in the final commit (the parent verifies it again):

| Run | Result |
|---|---|
| Edit Mode (default, `scripts/test-unity.ps1 -Suite EditMode`) | `UNITY_TEST_PASS suite=EditMode tests=2323 passed=2323 skipped=0` |
| `-Suite EditMode -Category BalanceFull` (with `IDLEFACTORY_WRITE_STRATEGY_BASELINE=1`) | `UNITY_TEST_PASS suite=EditMode tests=7 passed=7 skipped=0`; 13 definitions (12 + the report-only variant), 265 run-plays in 116 s of matrix time (PF-15 budget 20 min; the theoretical ceiling of the report, every run-play to the 6 h limit, projects 32 min and is over the budget, as it is a worst case no strategy approaches: the mean projection is 2.2 min) |
| Play Mode (full, `-Suite PlayMode`) | `UNITY_TEST_PASS suite=PlayMode tests=2147 passed=2147 skipped=0` (a first run found 37 failures that assumed revision 2 values, all fixed in the tests, section 12.1) |
| Windows player (`-StandaloneWindowsPlayer`) | `UNITY_TEST_PASS suite=PlayerPlayMode tests=33 passed=33 skipped=0` |

### 12.1 Tests changed because they assumed revision 2 values

Edit Mode: `RunContentCanonicalTests` (literal numbers, now revision 3; the worst-case figures are read from this record), `RunContentStructuralRuleTests` (the Overclock Core limit test, new bonus-floor and timed-member tests), `RunContentValidatorTests`, `RunContentSchemaConsistencyTests`, `RunContentReaderTests`, `ManualMiningTests` (the 100 % share cap needs a smaller yield where the example used the 150 % share as headroom), `V1ImportOnV3CatalogTests`, `W005LFixtureTests`, `PolicyR2Tests` (balanced mix 2 : 2 : 2 : 6, a 3,600 s window for the storage purchase, the tolerances of the Plate cooldown rule (2 looks to 3 % of the run) and of the casual diagnostic (2.0x to 1.5x, 1.43x run time), `BalanceTraceTests` (gate ids, the order of the two price variants, this record), `BalanceTraceFullTests`. Play Mode: `BuildPricePanelTests` (the growing-price variant resets the prestige growth), `BuildSheetPanelTests` (second Miner price from the curve), `ContractsAutoDeliverPanelTests` and `ContractsPanelTests` (cooldowns of Gear and timed Bar), `IndustryPanelTests` (Seed Capital decays), `MachinePanelGuidanceTests` (a Smelter that waits for Ore owns its guidance), `ContractsPanelTests` (derived 99 / 30), `W005PlayerTimingTests` and `SaveV3PlayerFixtureParityTests` (the new hash). No tolerance of a gate was widened; the changed test tolerances are derived in the test comments and in section 10.

`W005-L` regenerated (`W005LFixtureTests`, `IDLEFACTORY_REGENERATE_SAVEV3_FIXTURES=1`): 4,785 bytes, SHA-256 `7066eed15513d2bdeb2c349bfa98c2a3fdc500d6b179b997ed8fe92b954a70fd` for both the Edit Mode original and the StandalonePlayer copy (revision 2: 4,748 bytes, `ae620458...bf3e`); the shape is the run-3 row of the revision 3 trace (6 Miners at level 59, 2 Smelters at 54, 2 Presses and 2 Assemblers at 52, run-wide completion total 101). Timings in the Windows test player of this run (batch editor alive, two editor processes, a loaded host, so indicative only; the quiet-host gate is T349 and the emulator T351): the 8 h reconcile (algorithm) median **449.4 ms** of five runs (420.8 to 506.8, spread 19 %), the full cold-start return median **548.5 ms** (425.1 to 682.0, spread 47 %), against the 500 ms budget; 1,721,253 completions in the 8 h return (revision 2: 1,470,584), Save V3 write 0.39 ms in the mapper and 21.2 ms through the durable store. Revision 2 measured 233.8 ms and 274.1 ms in the quiet T336 run (231 and 272 ms in the test player with the editor alive). `w005l-measurement.md` carries the addendum. The Windows player test `W005PlayerTimingTests` pins the new hash.

Golden trace: `trace-r3.csv` regenerated with `IDLEFACTORY_REGENERATE_BALANCE_TRACE=1` (policy r2, content revision 3). Baseline report: `strategy-baseline-r3.md` regenerated with `IDLEFACTORY_WRITE_STRATEGY_BASELINE=1`.

```
scripts/test-unity.ps1 -Suite EditMode -Category BalanceFull
$env:IDLEFACTORY_WRITE_STRATEGY_BASELINE=1; scripts/test-unity.ps1 -Suite EditMode -Category BalanceFull
$env:IDLEFACTORY_REGENERATE_BALANCE_TRACE=1; scripts/test-unity.ps1 -TestFile Assets/IdleFactory/Tests/EditMode/Balance/BalanceTraceTests.cs
$env:IDLEFACTORY_REGENERATE_SAVEV3_FIXTURES=1; scripts/test-unity.ps1 -TestFile Assets/IdleFactory/Tests/EditMode/W005LFixtureTests.cs
```

## 13. Open questions for the owner (T354) and risks

OWNER decisions this record asks for; none blocks the commit.

1. **Human playtest (FR-027, F12).** The tuning rests on policy r2. Run 1 is 680 s longer than on revision 2 (7,148 s against 6,468 s), the first Gear arrives later (740 s against 660 s), and the chain now has 6 Miners in 12 slots. Please run the named playtest on this build before approving; the numbers here are orientation.
2. **Seed Capital size.** A player who buys the first five levels (70 IP) starts a run with 37 + 1,852 Cash, up to 37 + 2,540 after ten levels (a run's first Miner, Smelter and Press cost 30 + 9 + 21). Please confirm a starting bonus of that size is wanted, or lower the +500 (the model values it low either way, so the chain only buys it late).
3. **Contracts-only (P4c).** One Ore sale lets contracts-only build the whole factory and it still stalls at Factory Value 465,577 (section 6). The stall stays the SC-002 evidence. Confirm that no bootstrap exception, not even a rule that sells the surplus on a full store, is wanted.
4. **Validator follow-ups not done here:** a recipe cycle as a reported `chain.dominance` error instead of a silent skip, and a rule that rejects content without an input-free recipe (#379 question 4).
5. **`minGainPpm` unit (#396).** Two units (ppm of the base for factor kinds, ppm of the level-1 amount for additive kinds). Unification would need a decision and a validator change; none was made.
6. **Bounds.** Confirm the re-derived `contracts-off` bound (+350 per mille) and the `returns` rule (added wall time between 0 and the absence).

Risks: (1) the Play-Tester batch C (T346) and the human gate measure feel, not this record. (2) No Industry `maxLevel` shrinks (calibration 3 to 8, market contacts 2 to 5, toolmaker 1 to 3, Seed Capital 10 to 10, warehouse 2 to 2), because the permanent-state load path rejects a level above `maxLevel` (`TrySetIndustryLevel` returns `LevelMax`, the mapper fails the load): a shrink would be a drift class of its own. A first draft of this retune cut Seed Capital to 6 levels; it was set back to 10 for this reason. (3) The ECO-7 stock bound has 0.02 percentage points of room: a later raise of any sale-price source must lower another. (4) `W-later` and the single-lever margins are tight in runs 4 and 5 (1.21x for stop-at-Plate); a later Industry buff would narrow them. (5) **Performance (T349, F5-R14).** The regenerated W005-L return is about twice as slow as the revision 2 one in the same test player (449 ms against 231 ms for the algorithm, 549 ms against 272 ms for the full return; the event count rose only 17 %, so the cost per event rose too). The shape comes from the revision 3 trace row (6 Miners, 2 Smelters, 2 Presses, 2 Assemblers at cycles of about 1,000,000 ticks) and is a direct consequence of the 0.4x downstream cycles and the Miner-heavy mix. The run was on a loaded host, so it may overstate, but the full return is above the 500 ms budget in it: T349 must re-measure on a quiet host and decide between an optimisation of the offline reconciler and a decision on the workload; this task changes no production code. (6) The harness numbers equal the Unity numbers on revisions 2 and 3, but the Unity runs above are the evidence.
