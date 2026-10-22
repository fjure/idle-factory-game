# Strategy baseline on content revision 2, policy r2 (T341, issue #406)

**Orientation only. No number in sections 3, 5 and 6 is a band.** The asserted gates are the FR-024 list of `balance-policy-r2.md` section 8.2; section 4 lists their verdicts on this content, with the content-tuned gates that stay transitional until `contentRevision` 3 (T345, owner decision 2026-10-22, #408). Every number is a measurement of the engine on the content named here, by `StrategyBaselineReport` (`Assets/IdleFactory/Tests/EditMode/Balance/`), played by `BalanceTraceFullTests` (category `BalanceFull`). The matrix, the casual search and this file are produced only on opt-in: `$env:IDLEFACTORY_WRITE_STRATEGY_BASELINE=1; scripts/test-unity.ps1 -Suite EditMode -Category BalanceFull`. This file replaces `strategy-baseline-r2.md` (policy r1, kept as history).

## 1. Inputs

| Item | Value | Source |
|---|---|---|
| Build commit | `b1d264b84 (+ uncommitted changes under Assets/IdleFactory)` | `git rev-parse` in the generator |
| Content | `Assets/IdleFactory/Content/Iron/iron-factory-content.json`, `contentRevision` 2 (schema version 3, transitional revision 2 values) | loaded by `GameplayContentDispatcher.Load` |
| Policy | reference policy r2 (`ReferencePolicy`, `PolicyMix`, `IndustryModel`): balanced mix, net-worth income, storage on a `StorageFull` blocker read on arrival, `Sell` surplus dump, auto-deliver, held mining click, bonus offers, cooldowns and production objectives, IP by fluid-model gain per IP | `balance-policy-r2.md` |
| Strategies | 12 definitions: `chain`, `ore-only`, `contracts-only`, `idle-heavy`, `casual`, `prestige-later-1.5`, `prestige-later-3`, `stop-at-bar`, `stop-at-plate`, `market-only-chain`, `chain+mining`, `mining-only` | `balance-policy-r2.md` section 7 |
| Look grid | looks at run second `offset + k x L`; reference `L` = 20 s; away window 240 to 299 of every 300 s (idle-heavy: 120 to 299); casual `L` = 78 s | sections 2, 7 |
| Offsets | 5 per strategy: 0, 4, 8, 12, 16 s (casual: `i x L_casual / 5`: 0, 15, 31, 46, 62 s) | section 8.1 |
| Median | per run index over the offsets: third smallest, "not reached" ordered above every number; a `-` means fewer than 3 offsets have the value | `StrategyEnsemble.Median` |
| Time to threshold | run second of the first grid tick (idle ticks included) at which the prestige preview reports eligible; limit 21,600 s of run time | `BalanceTraceRunner.PlayRun` |
| Held mining click | `Mine` at every whole active run second after the first look: 100 % of the cap until the first Miner exists, afterwards 0 % (chain and most strategies), 25 % (casual), 100 % (chain+mining, mining-only) | section 6 |

## 2. Casual calibration (section 7.1)

`ReferencePolicy.CasualLookIntervalSeconds` = **78 s** [pinned, final]. The search on this build returns 78 s (equal to the pin).

- Rule: the smallest integer `L`, searched from 40 s in steps of 1 s, for which the median over the five casual offsets of the Factory Value at run second 900 of run 1 is at most 0.4 (1 / 2.5, the owner playtest of 2026-10-09) of the chain median at the same second. Policy r2, the held click at 25 % of the cap, content revision 2 values.
- Chain median Factory Value at second 900 (offsets 0, 4, 8, 12, 16): **8,716**.
- Casual median at `L` = 78 s: **3,462**; ratio **0.397** (limit 0.400).
- Candidates tried (L: casual median FV at 900 s): 40 s: 8234; 41 s: 8117; 42 s: 8491; 43 s: 8562; 44 s: 8078; 45 s: 8043; 46 s: 7660; 47 s: 7702; 48 s: 7333; 49 s: 6985; 50 s: 6864; 51 s: 6858; 52 s: 5596; 53 s: 5560; 54 s: 5263; 55 s: 6784; 56 s: 6703; 57 s: 6990; 58 s: 6895; 59 s: 6112; 60 s: 6622; 61 s: 6567; 62 s: 5856; 63 s: 5909; 64 s: 5853; 65 s: 5664; 66 s: 5310; 67 s: 5891; 68 s: 5056; 69 s: 5004; 70 s: 4589; 71 s: 4538; 72 s: 4462; 73 s: 4049; 74 s: 3608; 75 s: 3605; 76 s: 3545; 77 s: 3611; 78 s: 3462.
- The search is not monotone (the ratio is a step function of the look phase), which is why the pin is the first integer that satisfies the rule and is never moved afterwards.

## 3. Medians per strategy and run index

Columns (all medians over the offsets): time to eligibility (min to max beside it); first Gear; IP awarded at the prestige; Factory Value at run second 600, 900, 1,800, 3,600, 5,400 and 7,200 (a `-` where fewer than three offsets reached that second); net-worth growth per minute between second 900 and 1,800; cumulative play time C(n) to the prestige that completes run n (section 8.3); contracts delivered; reward-to-effective-value ratio per delivery (mean over the run's deliveries); share of income from contracts (contract Cash over contract Cash plus sales Cash); Ore mined by the held click and its share of income (mined Ore valued at its base price); machine-seconds held off by a contract (4.3); IP left unspent after the spending loop (5); commands issued in the run.

### chain

Rule: Reference policy r2: full chain, contracts, held mining click until the first Miner, prestige at the first non-idle look at or after eligibility.

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | NW/min 900-1800 | C(n) | Deliveries | Reward/eff. | Contract share | Mined Ore | Mining share | Held off (machine-s) | Unspent IP | Commands |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 6,468 s | 6,460 to 6,476 | 668 | 10 | 3,218 | 8,716 | 76,658 | 1,309,802 | 6,411,499 | - | 4,529 | 6,468 | 160 | 1.704 | 5.9 % | 0 | 0.0 % | 660 | 1 | 1,353 |
| 2 | 3,928 s | 3,920 to 3,936 | 528 | 10 | 11,708 | 37,326 | 428,073 | 7,974,572 | - | - | 26,049 | 10,396 | 99 | 1.703 | 4.3 % | 0 | 0.0 % | 680 | 8 | 1,135 |
| 3 | 3,652 s | 3,644 to 3,760 | 392 | 10 | 17,037 | 48,538 | 588,484 | 9,643,989 | - | - | 35,996 | 14,056 | 95 | 1.703 | 4.2 % | 0 | 0.0 % | 480 | 8 | 1,108 |
| 4 | 3,652 s | 3,644 to 3,760 | 392 | 10 | 17,037 | 48,538 | 588,484 | 9,643,989 | - | - | 35,996 | 17,708 | 95 | 1.703 | 4.2 % | 0 | 0.0 % | 480 | 3 | 1,108 |
| 5 | 3,648 s | 3,640 to 3,656 | 428 | 10 | 17,061 | 48,020 | 522,302 | 9,675,205 | - | - | 31,618 | 21,360 | 94 | 1.703 | 4.2 % | 0 | 0.0 % | 480 | 13 | 1,117 |

### ore-only

Rule: Only Miners are built; no contracts; all Ore sold by auto-sell; prestige at the threshold.

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | NW/min 900-1800 | C(n) | Deliveries | Reward/eff. | Contract share | Mined Ore | Mining share | Held off (machine-s) | Unspent IP | Commands |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 6,048 s | 6,040 to 6,056 | - | 10 | 4,641 | 9,507 | 68,311 | 1,072,441 | 5,755,351 | - | 3,920 | 6,048 | 0 | - | 0.0 % | 0 | 0.0 % | 0 | 1 | 670 |
| 2 | 2,748 s | 2,740 to 2,756 | - | 10 | 12,621 | 33,478 | 727,958 | - | - | - | 46,298 | 8,796 | 0 | - | 0.0 % | 0 | 0.0 % | 0 | 8 | 703 |
| 3 | 2,528 s | 2,520 to 2,536 | - | 10 | 20,201 | 61,830 | 968,179 | - | - | - | 60,423 | 11,324 | 0 | - | 0.0 % | 0 | 0.0 % | 0 | 8 | 700 |
| 4 | 2,528 s | 2,520 to 2,536 | - | 10 | 20,201 | 61,830 | 968,179 | - | - | - | 60,423 | 13,852 | 0 | - | 0.0 % | 0 | 0.0 % | 0 | 3 | 700 |
| 5 | 2,548 s | 2,360 to 2,556 | - | 10 | 20,258 | 59,914 | 862,022 | - | - | - | 53,473 | 16,400 | 0 | - | 0.0 % | 0 | 0.0 % | 0 | 13 | 702 |

### contracts-only

Rule: Chain machine mix; auto-sell off for every resource, no Sell; the only Cash from the factory is contract rewards.

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | NW/min 900-1800 | C(n) | Deliveries | Reward/eff. | Contract share | Mined Ore | Mining share | Held off (machine-s) | Unspent IP | Commands |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | not reached | all not reached | - | - | 629 | 929 | 1,037 | 1,037 | 1,037 | 1,037 | 7 | - | 0 | - | - | 0 | - | 0 | - | 1 |
| 2 | not reached | all not reached | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - |
| 3 | not reached | all not reached | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - |
| 4 | not reached | all not reached | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - |
| 5 | not reached | all not reached | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - |

### idle-heavy

Rule: Chain, but the player is away from second 120 to 299 of every 300 s (60 % idle).

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | NW/min 900-1800 | C(n) | Deliveries | Reward/eff. | Contract share | Mined Ore | Mining share | Held off (machine-s) | Unspent IP | Commands |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 8,868 s | 8,860 to 8,876 | 688 | 10 | 2,615 | 5,529 | 34,728 | 289,147 | 1,702,859 | 5,776,571 | 1,946 | 9,008 | 113 | 1.703 | 3.6 % | 0 | 0.0 % | 1,140 | 1 | 1,173 |
| 2 | 5,808 s | 5,800 to 5,816 | 388 | 10 | 8,081 | 22,810 | 127,419 | 1,226,534 | 7,731,594 | - | 6,973 | 14,816 | 75 | 1.703 | 3.1 % | 0 | 0.0 % | 780 | 8 | 1,040 |
| 3 | 5,788 s | 5,500 to 5,796 | 648 | 10 | 5,565 | 17,246 | 96,314 | 1,086,533 | 7,668,277 | - | 5,264 | 20,604 | 74 | 1.702 | 3.1 % | 0 | 0.0 % | 560 | 8 | 1,047 |
| 4 | 5,788 s | 5,500 to 5,796 | 648 | 10 | 5,565 | 17,246 | 96,314 | 1,086,533 | 7,668,277 | - | 5,264 | 26,392 | 74 | 1.702 | 3.1 % | 0 | 0.0 % | 560 | 3 | 1,047 |
| 5 | 5,728 s | 5,720 to 5,736 | 348 | 10 | 11,387 | 23,301 | 135,454 | 1,307,426 | 8,297,280 | - | 7,476 | 32,120 | 72 | 1.703 | 2.9 % | 0 | 0.0 % | 680 | 13 | 1,025 |

### casual

Rule: Chain with a look every 78 s (pinned L_casual) and the held mining click at 25 % of the cap.

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | NW/min 900-1800 | C(n) | Deliveries | Reward/eff. | Contract share | Mined Ore | Mining share | Held off (machine-s) | Unspent IP | Commands |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 12,464 s | 12,277 to 12,573 | 1,388 | 10 | 1,328 | 3,462 | 20,162 | 117,368 | 440,882 | 1,265,568 | 1,082 | 12,464 | 79 | 1.703 | 2.3 % | 2,962 | 0.0 % | 1,248 | 1 | 1,118 |
| 2 | 9,531 s | 9,500 to 9,640 | 795 | 10 | 4,634 | 12,256 | 56,693 | 265,571 | 1,269,523 | 3,166,988 | 2,979 | 22,088 | 62 | 1.702 | 2.4 % | 2,286 | 0.0 % | 702 | 8 | 939 |
| 3 | 9,406 s | 9,110 to 9,609 | 795 | 10 | 5,075 | 14,257 | 63,543 | 280,873 | 1,286,450 | 3,196,124 | 3,329 | 31,494 | 63 | 1.702 | 2.5 % | 2,234 | 0.0 % | 1,092 | 18 | 919 |
| 4 | 9,406 s | 9,110 to 9,609 | 795 | 10 | 5,075 | 14,257 | 63,543 | 280,873 | 1,286,450 | 3,196,124 | 3,329 | 40,900 | 63 | 1.702 | 2.5 % | 2,234 | 0.0 % | 1,092 | 28 | 918 |
| 5 | 9,406 s | 9,110 to 9,609 | 795 | 10 | 5,075 | 14,257 | 63,543 | 280,873 | 1,286,450 | 3,196,124 | 3,329 | 50,306 | 63 | 1.702 | 2.5 % | 2,234 | 0.0 % | 1,092 | 38 | 918 |

### prestige-later-1.5

Rule: Chain, but the prestige waits until Factory Value is 1.5 times the threshold (or the run limit).

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | NW/min 900-1800 | C(n) | Deliveries | Reward/eff. | Contract share | Mined Ore | Mining share | Held off (machine-s) | Unspent IP | Commands |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 6,468 s | 6,460 to 6,476 | 668 | 12 | 3,218 | 8,716 | 76,658 | 1,309,802 | 6,411,499 | 12,630,378 | 4,529 | 7,808 | 208 | 1.704 | 5.7 % | 0 | 0.0 % | 660 | 0 | 1,577 |
| 2 | 3,652 s | 3,644 to 3,760 | 392 | 12 | 17,037 | 48,538 | 588,484 | 9,643,989 | - | - | 35,996 | 12,024 | 115 | 1.703 | 3.6 % | 0 | 0.0 % | 480 | 2 | 1,234 |
| 3 | 3,652 s | 3,644 to 3,760 | 392 | 12 | 17,037 | 48,538 | 588,484 | 9,643,989 | - | - | 35,996 | 16,236 | 115 | 1.703 | 3.6 % | 0 | 0.0 % | 480 | 14 | 1,229 |
| 4 | 3,648 s | 3,640 to 3,656 | 428 | 12 | 17,061 | 48,020 | 522,302 | 9,675,205 | - | - | 31,618 | 20,468 | 115 | 1.703 | 3.6 % | 0 | 0.0 % | 480 | 11 | 1,243 |
| 5 | 3,648 s | 3,640 to 3,656 | 428 | 12 | 17,061 | 48,020 | 522,302 | 9,675,205 | - | - | 31,618 | 24,700 | 115 | 1.703 | 3.6 % | 0 | 0.0 % | 480 | 23 | 1,244 |

### prestige-later-3

Rule: Chain, but the prestige waits until Factory Value is 3 times the threshold (or the run limit).

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | NW/min 900-1800 | C(n) | Deliveries | Reward/eff. | Contract share | Mined Ore | Mining share | Held off (machine-s) | Unspent IP | Commands |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 6,468 s | 6,460 to 6,476 | 668 | 17 | 3,218 | 8,716 | 76,658 | 1,309,802 | 6,411,499 | 12,630,378 | 4,529 | 9,668 | 277 | 1.704 | 4.5 % | 0 | 0.0 % | 3,420 | 5 | 2,121 |
| 2 | 3,652 s | 3,644 to 3,760 | 392 | 17 | 17,037 | 48,538 | 588,484 | 9,643,989 | - | - | 35,996 | 14,484 | 137 | 1.704 | 2.2 % | 0 | 0.0 % | 1,120 | 12 | 1,469 |
| 3 | 3,652 s | 3,644 to 3,760 | 392 | 17 | 17,037 | 48,538 | 588,484 | 9,643,989 | - | - | 35,996 | 19,296 | 137 | 1.704 | 2.2 % | 0 | 0.0 % | 1,120 | 14 | 1,464 |
| 4 | 3,648 s | 3,640 to 3,656 | 428 | 17 | 17,061 | 48,020 | 522,302 | 9,675,205 | - | - | 31,618 | 24,108 | 136 | 1.704 | 2.2 % | 0 | 0.0 % | 1,320 | 31 | 1,469 |
| 5 | 3,608 s | 3,600 to 3,616 | 228 | 18 | 30,209 | 77,327 | 681,751 | 9,971,647 | - | - | 40,294 | 28,920 | 142 | 1.704 | 2.1 % | 0 | 0.0 % | 1,080 | 49 | 1,501 |

### stop-at-bar

Rule: Miner and Smelter only; the Iron Bar is the terminal and is sold; feasible contracts only.

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | NW/min 900-1800 | C(n) | Deliveries | Reward/eff. | Contract share | Mined Ore | Mining share | Held off (machine-s) | Unspent IP | Commands |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 4,488 s | 4,480 to 4,496 | - | 10 | 8,073 | 23,815 | 187,142 | 3,137,980 | - | - | 10,888 | 4,508 | 163 | 1.699 | 2.9 % | 0 | 0.0 % | 0 | 1 | 1,185 |
| 2 | 3,028 s | 3,020 to 3,036 | - | 10 | 21,479 | 47,958 | 408,548 | - | - | - | 24,039 | 7,536 | 110 | 1.699 | 1.9 % | 0 | 0.0 % | 0 | 8 | 1,053 |
| 3 | 2,928 s | 2,920 to 2,936 | - | 10 | 24,974 | 58,917 | 519,732 | - | - | - | 30,721 | 10,464 | 113 | 1.699 | 2.0 % | 0 | 0.0 % | 0 | 8 | 1,048 |
| 4 | 2,928 s | 2,920 to 2,936 | - | 10 | 24,974 | 58,917 | 519,732 | - | - | - | 30,721 | 13,392 | 113 | 1.699 | 2.0 % | 0 | 0.0 % | 0 | 3 | 1,048 |
| 5 | 2,848 s | 2,840 to 2,856 | - | 10 | 31,114 | 70,204 | 629,626 | - | - | - | 37,294 | 16,240 | 110 | 1.699 | 1.9 % | 0 | 0.0 % | 0 | 13 | 1,037 |

### stop-at-plate

Rule: Miner, Smelter and Press only; the Iron Plate is the terminal and is sold; feasible contracts only.

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | NW/min 900-1800 | C(n) | Deliveries | Reward/eff. | Contract share | Mined Ore | Mining share | Held off (machine-s) | Unspent IP | Commands |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 6,628 s | 6,620 to 6,636 | - | 10 | 3,349 | 8,689 | 91,510 | 1,275,955 | 5,960,854 | - | 5,521 | 6,628 | 191 | 1.712 | 3.4 % | 0 | 0.0 % | 240 | 1 | 1,432 |
| 2 | 4,348 s | 4,340 to 4,356 | - | 10 | 12,846 | 37,353 | 305,085 | 6,218,987 | - | - | 17,848 | 10,976 | 131 | 1.712 | 2.6 % | 0 | 0.0 % | 580 | 8 | 1,228 |
| 3 | 3,932 s | 3,924 to 4,080 | - | 10 | 21,358 | 60,191 | 611,152 | 8,169,745 | - | - | 36,730 | 14,916 | 123 | 1.712 | 2.4 % | 0 | 0.0 % | 200 | 8 | 1,189 |
| 4 | 3,932 s | 3,924 to 4,080 | - | 10 | 21,358 | 60,191 | 611,152 | 8,169,745 | - | - | 36,730 | 18,848 | 123 | 1.712 | 2.4 % | 0 | 0.0 % | 200 | 3 | 1,189 |
| 5 | 3,948 s | 3,940 to 3,956 | - | 10 | 27,685 | 70,595 | 555,947 | 8,049,892 | - | - | 32,356 | 22,800 | 123 | 1.712 | 2.4 % | 0 | 0.0 % | 260 | 13 | 1,192 |

### market-only-chain

Rule: Chain with contracts off: no AcceptContract, no SetAutoDeliver, no Items from the ladder.

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | NW/min 900-1800 | C(n) | Deliveries | Reward/eff. | Contract share | Mined Ore | Mining share | Held off (machine-s) | Unspent IP | Commands |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 8,208 s | 8,200 to 8,216 | 948 | 10 | 1,862 | 4,691 | 35,515 | 419,082 | 2,307,382 | 6,409,340 | 2,054 | 8,208 | 0 | - | 0.0 % | 0 | 0.0 % | 0 | 1 | 1,092 |
| 2 | 4,368 s | 4,360 to 4,376 | 488 | 10 | 10,499 | 26,542 | 375,199 | 6,475,832 | - | - | 23,243 | 12,576 | 0 | - | 0.0 % | 0 | 0.0 % | 0 | 8 | 902 |
| 3 | 3,972 s | 3,964 to 4,000 | 408 | 10 | 13,415 | 39,343 | 534,410 | 8,024,619 | - | - | 33,015 | 16,556 | 0 | - | 0.0 % | 0 | 0.0 % | 0 | 8 | 873 |
| 4 | 3,972 s | 3,964 to 4,000 | 408 | 10 | 13,415 | 39,343 | 534,410 | 8,024,619 | - | - | 33,015 | 20,528 | 0 | - | 0.0 % | 0 | 0.0 % | 0 | 3 | 873 |
| 5 | 3,928 s | 3,920 to 3,936 | 348 | 10 | 18,313 | 49,999 | 603,729 | 8,374,696 | - | - | 36,915 | 24,460 | 0 | - | 0.0 % | 0 | 0.0 % | 0 | 13 | 881 |

### chain+mining

Rule: Chain, but the held click mines the whole per-second cap in every active second of every run.

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | NW/min 900-1800 | C(n) | Deliveries | Reward/eff. | Contract share | Mined Ore | Mining share | Held off (machine-s) | Unspent IP | Commands |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 6,468 s | 6,400 to 6,476 | 688 | 10 | 3,607 | 10,185 | 82,346 | 1,370,921 | 6,476,125 | - | 4,804 | 6,468 | 160 | 1.704 | 5.9 % | 5,070 | 0.1 % | 720 | 1 | 1,356 |
| 2 | 4,104 s | 4,020 to 4,116 | 528 | 10 | 9,755 | 29,852 | 317,516 | 6,770,308 | - | - | 19,164 | 10,568 | 102 | 1.704 | 4.5 % | 3,242 | 0.0 % | 920 | 8 | 1,173 |
| 3 | 3,628 s | 3,620 to 3,692 | 432 | 10 | 16,907 | 43,399 | 577,570 | 9,924,930 | - | - | 35,611 | 14,192 | 95 | 1.703 | 4.1 % | 2,849 | 0.0 % | 560 | 8 | 1,115 |
| 4 | 3,628 s | 3,620 to 3,692 | 432 | 10 | 16,907 | 43,399 | 577,570 | 9,924,930 | - | - | 35,611 | 17,816 | 95 | 1.703 | 4.1 % | 2,849 | 0.0 % | 560 | 3 | 1,115 |
| 5 | 3,600 s | 3,592 to 3,644 | 408 | 10 | 28,266 | 73,009 | 604,548 | 10,055,618 | - | - | 35,410 | 21,460 | 93 | 1.703 | 4.0 % | 2,822 | 0.0 % | 520 | 13 | 1,116 |

### mining-only

Rule: Builds nothing; mines the whole per-second cap in every active second; sells by auto-sell; buys Industry.

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | NW/min 900-1800 | C(n) | Deliveries | Reward/eff. | Contract share | Mined Ore | Mining share | Held off (machine-s) | Unspent IP | Commands |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | not reached | all not reached | - | - | 509 | 749 | 1,469 | 2,909 | 4,349 | 5,789 | 48 | - | 0 | - | 0.0 % | 17,280 | 100.0 % | 0 | - | 1 |
| 2 | not reached | all not reached | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - |
| 3 | not reached | all not reached | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - |
| 4 | not reached | all not reached | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - |
| 5 | not reached | all not reached | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - |

## 4. Gates (balance-policy-r2.md section 8.2)

A gate marked **transitional** is content-tuned: it is implemented and measured, fails on the revision 2 values, and asserts from `contentRevision` 3 (`TraceGates.AssertsFromRevision`). Every other gate asserts now.

| Gate | Verdict | Status at revision 2 | Measured |
|---|---|---|---|
| `conservation` Conservation and exact per-command deltas | pass | asserted (passes) | 0 violations over 60 histories |
| `sale-neutrality` Sale value neutrality (FR-002) | pass | asserted (passes) | 38,791 checked Sells (4,374 lost 1 to the floor, 0 out of rule), 45 unchecked |
| `partition-invariance` Auto-deliver partition invariance (SC-003) | pass | asserted (passes) | identical state for 2 strategies, 19 contract completions compared |
| `remove-rebuild` Remove/rebuild (ECO-7) | pass | asserted (passes) | 10 machines replaced, largest change -153 |
| `eco-3` Restated ECO-3 (FR-021) | pass | asserted (passes) | 6,468 s / 3,928 s / 3,652 s / 3,652 s / 3,648 s |
| `ratio-bounds` Contract ratio bounds on base and effective prices (FR-004, D-D) | FAIL | **transitional** (reported, asserts at revision 3) | 23,105 deliveries, effective ratio 1.500 to 1.750; 22,930 beyond the configured bound, 22,930 beyond the effective bound (contract.gear (effective), contract.gear (ratio 1.667), contract.plate (effective), contract.plate (ratio 1.75), contract.timed-bar (effective), contract.timed-bar (ratio 1.7)) |
| `item-uptime` Item uptime (ECO-6, ladder.uptime) | FAIL | **transitional** (reported, asserts at revision 3) | ladder.uptime at revision 3: fails for item.overclock-core; most Items granted in one run 10 (bound 10) |
| `start-affordability` Start affordability (FR-016, SC-011) | pass | asserted (passes) | reachable at every run start, smallest margin 67 Cash |
| `chain-dominance` Chain dominance per slot (ECO-12, chain.dominance) | FAIL | **transitional** (reported, asserts at revision 3) | 3 diagnostics at revision 3 (iron.gear, recipe.press-iron-plate, recipe.smelt-iron-bar) |
| `single-lever` No single-lever strategy faster than the chain (ECO-13) | FAIL | **transitional** (reported, asserts at revision 3) | 10 misses (ore-only 0.94/0.70/0.69/0.69/0.70; stop-at-bar 0.69/0.77/0.80/0.80/0.78; stop-at-plate 1.02/1.11/1.08/1.08/1.08) |
| `contracts-only-longer` Contracts-only strictly longer than the chain (SC-002) | pass | asserted (passes) | run 1 not reached |
| `chain-within-6h` The chain reaches every threshold within 6 simulated hours | pass | asserted (passes) | slowest 6,476 s |
| `ip-4x` 4.0x rule: practical Industry ceiling over the highest IP (FR-021, ECO-9) | pass | asserted (passes) | ceiling 1,263 IP, highest IP 17 (prestige-later-3 run 1 offset 0), ratio 74.3 |
| `chain-plus-mining` Chain+mining not faster than the chain by more than miningAdvantageMaxPpm (MIN-9) | pass | asserted (passes) | advantage per run 0.0 % / -4.4 % / 0.6 % / 0.6 % / 1.3 % |

## 5. Orientation: ratios, warnings and reports (never gates)

Median time to eligibility relative to the chain (strategy median over chain median of the same run index; below 1.00x means faster than the chain):

| Strategy | Run 1 | Run 2 | Run 3 | Run 4 | Run 5 |
|---|---|---|---|---|---|
| `ore-only` | 0.94x | 0.70x | 0.69x | 0.69x | 0.70x |
| `contracts-only` | not reached | not reached | not reached | not reached | not reached |
| `idle-heavy` | 1.37x | 1.48x | 1.58x | 1.58x | 1.57x |
| `casual` | 1.93x | 2.43x | 2.58x | 2.58x | 2.58x |
| `prestige-later-1.5` | 1.00x | 0.93x | 1.00x | 1.00x | 1.00x |
| `prestige-later-3` | 1.00x | 0.93x | 1.00x | 1.00x | 0.99x |
| `stop-at-bar` | 0.69x | 0.77x | 0.80x | 0.80x | 0.78x |
| `stop-at-plate` | 1.02x | 1.11x | 1.08x | 1.08x | 1.08x |
| `market-only-chain` | 1.27x | 1.11x | 1.09x | 1.09x | 1.08x |
| `chain+mining` | 1.00x | 1.04x | 0.99x | 0.99x | 0.99x |
| `mining-only` | not reached | not reached | not reached | not reached | not reached |

Cumulative play time C(n) to the prestige that completes run n (median over offsets) and the warning **W-later** (section 8.3):

| Strategy | C(1) | C(2) | C(3) | C(4) | C(5) |
|---|---|---|---|---|---|
| `chain` | 6,468 | 10,396 | 14,056 | 17,708 | 21,360 |
| `prestige-later-1.5` | 7,808 | 12,024 | 16,236 | 20,468 | 24,700 |
| `prestige-later-3` | 9,668 | 14,484 | 19,296 | 24,108 | 28,920 |

- W-later: not raised (waiting never beats prestiging at the threshold in cumulative time).
- W-market (P4a): not raised (contracts never cost the chain time against the market-only chain).
- W-mining (P4b): not raised. `mining-only` never reaches a threshold within 21,600 s (highest Factory Value read at a checkpoint 5,797); it mined 17,280 Ore per offset in run 1.
- P4c, contracts-only: run 1 not reached (Factory Value at 1,800 s: 1,037). It stalls by construction in run 1; the last checkpoint at which its Factory Value still changed is run second 1,800: auto-sell is off, the starting Cash buys one Miner, and every contract needs Bars, Plates or Gears. Its income is 100 % contracts by definition (0 Cash per minute). No bootstrap exception is applied: none was decided (open question for the owner).

Measured completions per run (the basis for `perRunCap` and the reachable bound of `contract.quantity.capacity`, #387 (a) and (d)); median of the chain over the offsets, and the highest count of any strategy, offset and run. The required units at the highest count are `quantity[count]` of the offer; the top storage capacity is 16,000.

| Offer | Chain run 1 | Run 2 | Run 3 | Run 4 | Run 5 | Highest in the matrix | Required units there |
|---|---|---|---|---|---|---|---|
| `contract.bonus-plate` | 1 | 1 | 1 | 1 | 1 | 1 | 100 |
| `contract.gear` | 53 | 32 | 31 | 31 | 31 | 92 | 331 |
| `contract.plate` | 53 | 33 | 31 | 31 | 31 | 92 | 331 |
| `contract.produce-plate` | 17 | 11 | 10 | 10 | 10 | 27 | 47 |
| `contract.timed-bar` | 53 | 33 | 32 | 32 | 31 | 163 | 719 |

Items granted per run (one Item per tier in the shipped content): chain median tier 1 / tier 2 / tier 3, and the highest of any run: run 1 4/3/3; run 2 4/3/3; run 3 4/3/3; run 4 4/3/3; run 5 4/3/3; highest 4/3/3. The ladder has 10 entries and `perRunCap` 10.
- Unspent IP and zero-gain levels (section 5): an affordable level whose fluid-model gain is not positive is never bought and is recorded (`industry_zero_gain.<id>`). Recorded in this matrix: `industry.seed-capital` (casual, prestige-later-1.5, prestige-later-3); `industry.warehouse` (casual, prestige-later-1.5, prestige-later-3). The model never values `industry.warehouse` (capacity has no effect in a model without a storage limit), which is the stated design. Medians of the unspent IP are in section 3.
- Mining income share, MIN-9 (sections 3 and 4): `chain+mining` holds the click at the whole cap in every active second; its advantage over the chain per run index is in the `chain-plus-mining` gate.
- Reward-to-effective-value ratio per delivery: see the `ratio-bounds` gate (observed range over every delivery in the matrix).

## 6. Runtime and the projection of the full matrix (PF-15), measured on policy r2

Wall-clock milliseconds of one history (runs 1 to 5, prestige and IP spending included) per strategy and offset, measured with `Stopwatch` around `BalanceTraceRunner.PlayRun` in a single-threaded Unity Edit Mode run on this workstation (`IndustryModel` memoised on its key; histories not parallelised). Load on the machine changes these numbers; the figures are an order of magnitude, not a benchmark.

| Strategy | Offset 1 | Offset 2 | Offset 3 | Offset 4 | Offset 5 | Strategy total | Run-plays | ms per run-play |
|---|---|---|---|---|---|---|---|---|
| chain | 2,945 ms (offset 0 s, 5 runs) | 967 ms (offset 4 s, 5 runs) | 961 ms (offset 8 s, 5 runs) | 957 ms (offset 12 s, 5 runs) | 964 ms (offset 16 s, 5 runs) | 6,794 ms | 25 | 272 |
| ore-only | 2,032 ms (offset 0 s, 5 runs) | 731 ms (offset 4 s, 5 runs) | 723 ms (offset 8 s, 5 runs) | 727 ms (offset 12 s, 5 runs) | 721 ms (offset 16 s, 5 runs) | 4,934 ms | 25 | 197 |
| contracts-only | 18 ms (offset 0 s, 1 runs) | 25 ms (offset 4 s, 1 runs) | 20 ms (offset 8 s, 1 runs) | 18 ms (offset 12 s, 1 runs) | 18 ms (offset 16 s, 1 runs) | 99 ms | 5 | 20 |
| idle-heavy | 3,046 ms (offset 0 s, 5 runs) | 1,084 ms (offset 4 s, 5 runs) | 1,080 ms (offset 8 s, 5 runs) | 1,069 ms (offset 12 s, 5 runs) | 1,090 ms (offset 16 s, 5 runs) | 7,369 ms | 25 | 295 |
| casual | 4,819 ms (offset 0 s, 5 runs) | 2,641 ms (offset 15 s, 5 runs) | 2,613 ms (offset 31 s, 5 runs) | 2,618 ms (offset 46 s, 5 runs) | 2,591 ms (offset 62 s, 5 runs) | 15,282 ms | 25 | 611 |
| prestige-later-1.5 | 1,705 ms (offset 0 s, 5 runs) | 1,123 ms (offset 4 s, 5 runs) | 1,131 ms (offset 8 s, 5 runs) | 1,128 ms (offset 12 s, 5 runs) | 1,131 ms (offset 16 s, 5 runs) | 6,218 ms | 25 | 249 |
| prestige-later-3 | 1,914 ms (offset 0 s, 5 runs) | 1,313 ms (offset 4 s, 5 runs) | 1,331 ms (offset 8 s, 5 runs) | 1,340 ms (offset 12 s, 5 runs) | 1,323 ms (offset 16 s, 5 runs) | 7,221 ms | 25 | 289 |
| stop-at-bar | 2,349 ms (offset 0 s, 5 runs) | 804 ms (offset 4 s, 5 runs) | 801 ms (offset 8 s, 5 runs) | 811 ms (offset 12 s, 5 runs) | 785 ms (offset 16 s, 5 runs) | 5,550 ms | 25 | 222 |
| stop-at-plate | 2,784 ms (offset 0 s, 5 runs) | 1,020 ms (offset 4 s, 5 runs) | 1,018 ms (offset 8 s, 5 runs) | 1,004 ms (offset 12 s, 5 runs) | 1,013 ms (offset 16 s, 5 runs) | 6,839 ms | 25 | 274 |
| market-only-chain | 1,015 ms (offset 0 s, 5 runs) | 1,034 ms (offset 4 s, 5 runs) | 998 ms (offset 8 s, 5 runs) | 996 ms (offset 12 s, 5 runs) | 1,000 ms (offset 16 s, 5 runs) | 5,043 ms | 25 | 202 |
| chain+mining | 3,423 ms (offset 0 s, 5 runs) | 1,508 ms (offset 4 s, 5 runs) | 1,522 ms (offset 8 s, 5 runs) | 1,550 ms (offset 12 s, 5 runs) | 1,522 ms (offset 16 s, 5 runs) | 9,525 ms | 25 | 381 |
| mining-only | 71 ms (offset 0 s, 1 runs) | 75 ms (offset 4 s, 1 runs) | 73 ms (offset 8 s, 1 runs) | 73 ms (offset 12 s, 1 runs) | 73 ms (offset 16 s, 1 runs) | 365 ms | 5 | 73 |

Measured: 260 run-plays (12 strategies x 5 offsets x up to 5 runs) in 75.2 s of history time = 1.25 min, mean 289 ms per run-play, worst strategy `casual` 611 ms per run-play. Matrix wall time: 75.4 s. `IndustryModel`: 178 evaluations computed, 1,442 served from the memo.

| Projection of the full matrix (12 definitions x 5 offsets x 5 runs = 300 run-plays; sequential) | Mean-based | Worst-strategy-based | Against the 20 min budget |
|---|---|---|---|
| Projected from this run | 1.4 min | 3.1 min | within the budget |
| Ceiling: every run-play runs to the 21,600 s limit at the highest measured cost per simulated second (0.161 ms/s) | - | 17.4 min | within the budget |

Decision rule of `balance-policy-r2.md` section 8.1: if a projection exceeds the budget, memoise `T_fluid` and run histories in parallel first, then move only `mining-only` to an opt-in category; the offset count stays five and no FR-023 strategy is moved. The r1 projection of T311 (`strategy-baseline-r2.md` section 6) was 2.2 min mean and 3.5 min worst for the same 300 run-plays on five measured strategies.
