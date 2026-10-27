# Strategy baseline on content revision 3, policy r2 (T341, issue #406; regenerated for T345, issue #410)

**Orientation only. No number in sections 3, 5 and 6 is a band.** The asserted gates are the FR-024 list of `balance-policy-r2.md` section 8.2; section 4 lists their verdicts on this content: at `contentRevision` 3 every gate asserts and none is transitional. Every number is a measurement of the engine on the content named here, by `StrategyBaselineReport` (`Assets/IdleFactory/Tests/EditMode/Balance/`), played by `BalanceTraceFullTests` (category `BalanceFull`). The matrix, the casual search and this file are produced only on opt-in: `$env:IDLEFACTORY_WRITE_STRATEGY_BASELINE=1; scripts/test-unity.ps1 -Suite EditMode -Category BalanceFull`. This file replaces `strategy-baseline-r2.md` (policy r1, kept as history).

## 1. Inputs

| Item | Value | Source |
|---|---|---|
| Build commit | `c299e8483 (+ uncommitted changes under Assets/IdleFactory)` | `git rev-parse` in the generator |
| Content | `Assets/IdleFactory/Content/Iron/iron-factory-content.json`, `contentRevision` 3 (schema version 3, the tuned revision 3 values of T345) | loaded by `GameplayContentDispatcher.Load` |
| Policy | reference policy r2 (`ReferencePolicy`, `PolicyMix`, `IndustryModel`): balanced mix, net-worth income, storage on a `StorageFull` blocker read on arrival, `Sell` surplus dump, auto-deliver, held mining click, bonus offers, cooldowns and production objectives, IP by fluid-model gain per IP | `balance-policy-r2.md` |
| Strategies | 13 definitions: `chain`, `ore-only`, `contracts-only`, `idle-heavy`, `casual`, `prestige-later-1.5`, `prestige-later-3`, `stop-at-bar`, `stop-at-plate`, `market-only-chain`, `chain+mining`, `mining-only`, `contracts-only+sale` (the last is report-only: no gate, owner decision 2026-10-22) | `balance-policy-r2.md` section 7 |
| Look grid | looks at run second `offset + k x L`; reference `L` = 20 s; away window 240 to 299 of every 300 s (idle-heavy: 120 to 299); casual `L` = 78 s | sections 2, 7 |
| Offsets | 5 per strategy: 0, 4, 8, 12, 16 s (casual: `i x L_casual / 5`: 0, 15, 31, 46, 62 s) | section 8.1 |
| Median | per run index over the offsets: third smallest, "not reached" ordered above every number; a `-` means fewer than 3 offsets have the value | `StrategyEnsemble.Median` |
| Time to threshold | run second of the first grid tick (idle ticks included) at which the prestige preview reports eligible; limit 21,600 s of run time | `BalanceTraceRunner.PlayRun` |
| Held mining click | `Mine` at every whole active run second after the first look: 100 % of the cap until the first Miner exists, afterwards 0 % (chain and most strategies), 25 % (casual), 100 % (chain+mining, mining-only) | section 6 |

## 2. Casual calibration (section 7.1)

`ReferencePolicy.CasualLookIntervalSeconds` = **78 s** [pinned, final]. The search on this build returns 76 s (different from the pin: the pin is not recalibrated).

- Rule: the smallest integer `L`, searched from 40 s in steps of 1 s, for which the median over the five casual offsets of the Factory Value at run second 900 of run 1 is at most 0.4 (1 / 2.5, the owner playtest of 2026-10-09) of the chain median at the same second. Policy r2, the held click at 25 % of the cap, content revision 2 values (the pin was found on them and is never recalibrated; on a later revision the ratio drifts and is orientation).
- Chain median Factory Value at second 900 (offsets 0, 4, 8, 12, 16): **11,156**.
- Casual median at `L` = 76 s: **4,155**; ratio **0.372** (limit 0.400).
- Candidates tried (L: casual median FV at 900 s): 40 s: 8963; 41 s: 9146; 42 s: 9391; 43 s: 9431; 44 s: 8930; 45 s: 9053; 46 s: 8921; 47 s: 8767; 48 s: 8408; 49 s: 7522; 50 s: 8750; 51 s: 8462; 52 s: 7429; 53 s: 7356; 54 s: 7170; 55 s: 7020; 56 s: 7193; 57 s: 7151; 58 s: 7176; 59 s: 7211; 60 s: 7140; 61 s: 6725; 62 s: 6593; 63 s: 5760; 64 s: 5743; 65 s: 5807; 66 s: 6489; 67 s: 6213; 68 s: 5943; 69 s: 5988; 70 s: 5645; 71 s: 5459; 72 s: 5487; 73 s: 4921; 74 s: 4882; 75 s: 4834; 76 s: 4155.
- The search is not monotone (the ratio is a step function of the look phase), which is why the pin is the first integer that satisfies the rule and is never moved afterwards.

## 3. Medians per strategy and run index

Columns (all medians over the offsets): time to eligibility (min to max beside it); first Gear; IP awarded at the prestige; Factory Value at run second 600, 900, 1,800, 3,600, 5,400 and 7,200 (a `-` where fewer than three offsets reached that second); net-worth growth per minute between second 900 and 1,800; cumulative play time C(n) to the prestige that completes run n (section 8.3); contracts delivered; reward-to-effective-value ratio per delivery (mean over the run's deliveries); share of income from contracts (contract Cash over contract Cash plus sales Cash); Ore mined by the held click and its share of income (mined Ore valued at its base price); machine-seconds held off by a contract (4.3); IP left unspent after the spending loop (5); commands issued in the run.

### chain

Rule: Reference policy r2: full chain, contracts, held mining click until the first Miner, prestige at the first non-idle look at or after eligibility.

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | NW/min 900-1800 | C(n) | Deliveries | Reward/eff. | Contract share | Mined Ore | Mining share | Held off (machine-s) | Unspent IP | Commands |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 7,148 s | 7,140 to 7,156 | 748 | 10 | 3,844 | 11,156 | 95,687 | 1,331,076 | 4,302,316 | 10,169,882 | 5,635 | 7,208 | 163 | 1.502 | 5.7 % | 0 | 0.0 % | 1,200 | 2 | 1,341 |
| 2 | 4,588 s | 4,580 to 4,596 | 468 | 10 | 14,243 | 49,060 | 380,194 | 5,312,163 | - | - | 22,075 | 11,796 | 110 | 1.502 | 3.4 % | 0 | 0.0 % | 640 | 1 | 1,099 |
| 3 | 3,808 s | 3,720 to 3,816 | 388 | 11 | 26,431 | 70,709 | 704,733 | 9,941,384 | - | - | 42,268 | 15,604 | 93 | 1.503 | 2.7 % | 0 | 0.0 % | 600 | 2 | 1,081 |
| 4 | 3,608 s | 3,600 to 3,616 | 388 | 11 | 30,576 | 72,006 | 598,398 | 13,270,163 | - | - | 35,092 | 19,212 | 87 | 1.503 | 2.5 % | 0 | 0.0 % | 720 | 0 | 1,067 |
| 5 | 3,408 s | 3,400 to 3,416 | 388 | 12 | 34,751 | 78,847 | 840,337 | - | - | - | 50,766 | 22,620 | 79 | 1.504 | 1.9 % | 0 | 0.0 % | 360 | 4 | 1,017 |

### ore-only

Rule: Only Miners are built; no contracts; all Ore sold by auto-sell; prestige at the threshold.

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | NW/min 900-1800 | C(n) | Deliveries | Reward/eff. | Contract share | Mined Ore | Mining share | Held off (machine-s) | Unspent IP | Commands |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 17,428 s | 17,420 to 17,436 | - | 10 | 3,571 | 7,247 | 36,165 | 208,637 | 713,649 | 1,473,689 | 1,927 | 17,428 | 0 | - | 0.0 % | 0 | 0.0 % | 0 | 2 | 659 |
| 2 | 10,548 s | 10,540 to 10,556 | - | 10 | 7,953 | 20,425 | 113,107 | 865,814 | 2,363,616 | 4,505,420 | 6,178 | 27,976 | 0 | - | 0.0 % | 0 | 0.0 % | 0 | 1 | 644 |
| 3 | 7,868 s | 7,860 to 7,876 | - | 11 | 11,001 | 23,675 | 173,099 | 1,714,247 | 4,489,118 | 9,435,514 | 9,961 | 35,844 | 0 | - | 0.0 % | 0 | 0.0 % | 0 | 2 | 649 |
| 4 | 6,888 s | 6,840 to 6,896 | - | 11 | 13,107 | 32,311 | 223,132 | 2,369,678 | 6,426,423 | - | 12,721 | 42,752 | 0 | - | 0.0 % | 0 | 0.0 % | 0 | 0 | 661 |
| 5 | 6,168 s | 6,140 to 6,176 | - | 12 | 19,643 | 56,538 | 508,661 | 3,469,159 | 9,577,597 | - | 30,141 | 48,920 | 0 | - | 0.0 % | 0 | 0.0 % | 0 | 4 | 669 |

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
| 1 | 8,668 s | 8,660 to 8,676 | 928 | 10 | 3,751 | 7,053 | 55,272 | 581,143 | 2,454,921 | 5,890,941 | 3,214 | 8,708 | 96 | 1.503 | 2.8 % | 0 | 0.0 % | 580 | 2 | 1,059 |
| 2 | 6,208 s | 6,200 to 6,216 | 628 | 10 | 6,490 | 16,290 | 113,995 | 1,448,537 | 6,884,851 | - | 6,513 | 15,016 | 70 | 1.504 | 1.9 % | 0 | 0.0 % | 660 | 1 | 951 |
| 3 | 5,592 s | 5,584 to 6,080 | 668 | 11 | 8,785 | 29,461 | 143,765 | 1,897,874 | 10,091,941 | - | 7,620 | 20,736 | 63 | 1.504 | 1.5 % | 0 | 0.0 % | 680 | 2 | 947 |
| 4 | 5,668 s | 5,660 to 5,676 | 688 | 11 | 9,435 | 23,911 | 103,241 | 1,537,829 | 9,466,683 | - | 5,288 | 26,448 | 63 | 1.505 | 1.6 % | 0 | 0.0 % | 720 | 0 | 954 |
| 5 | 5,328 s | 5,320 to 5,336 | 688 | 12 | 10,333 | 31,028 | 194,877 | 2,329,069 | 16,026,641 | - | 10,923 | 31,860 | 58 | 1.505 | 1.2 % | 0 | 0.0 % | 500 | 4 | 952 |

### casual

Rule: Chain with a look every 78 s (pinned L_casual) and the held mining click at 25 % of the cap.

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | NW/min 900-1800 | C(n) | Deliveries | Reward/eff. | Contract share | Mined Ore | Mining share | Held off (machine-s) | Unspent IP | Commands |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 12,012 s | 11,809 to 12,682 | 1,201 | 10 | 1,792 | 4,190 | 29,063 | 157,018 | 433,466 | 1,389,570 | 1,666 | 12,012 | 87 | 1.503 | 2.4 % | 2,791 | 0.0 % | 4,602 | 2 | 1,063 |
| 2 | 10,358 s | 9,859 to 10,420 | 873 | 10 | 5,471 | 11,720 | 56,814 | 276,654 | 842,873 | 2,746,245 | 2,996 | 22,276 | 76 | 1.504 | 2.0 % | 2,388 | 0.0 % | 3,276 | 1 | 1,000 |
| 3 | 9,906 s | 9,578 to 10,015 | 873 | 11 | 6,577 | 14,191 | 67,238 | 371,865 | 1,109,258 | 3,455,585 | 3,541 | 32,136 | 73 | 1.504 | 1.9 % | 2,333 | 0.0 % | 3,198 | 2 | 974 |
| 4 | 9,999 s | 9,718 to 10,218 | 873 | 11 | 7,273 | 15,585 | 76,364 | 339,816 | 1,099,014 | 3,570,825 | 4,007 | 42,354 | 73 | 1.504 | 1.9 % | 2,355 | 0.0 % | 3,120 | 0 | 976 |
| 5 | 10,046 s | 9,952 to 10,218 | 592 | 12 | 8,205 | 19,352 | 89,910 | 388,960 | 1,213,236 | 4,071,297 | 4,676 | 52,490 | 73 | 1.504 | 1.8 % | 2,340 | 0.0 % | 3,588 | 4 | 984 |

### prestige-later-1.5

Rule: Chain, but the prestige waits until Factory Value is 1.5 times the threshold (or the run limit).

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | NW/min 900-1800 | C(n) | Deliveries | Reward/eff. | Contract share | Mined Ore | Mining share | Held off (machine-s) | Unspent IP | Commands |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 7,148 s | 7,140 to 7,156 | 748 | 12 | 3,844 | 11,156 | 95,687 | 1,331,076 | 4,302,316 | 10,169,882 | 5,635 | 8,248 | 191 | 1.501 | 4.8 % | 0 | 0.0 % | 1,200 | 1 | 1,513 |
| 2 | 4,428 s | 4,420 to 4,436 | 468 | 12 | 14,915 | 42,293 | 301,022 | 5,411,634 | - | - | 17,248 | 13,116 | 114 | 1.502 | 2.4 % | 0 | 0.0 % | 680 | 0 | 1,189 |
| 3 | 3,572 s | 3,564 to 3,620 | 388 | 13 | 27,859 | 70,573 | 685,525 | 12,604,453 | - | - | 40,996 | 17,036 | 93 | 1.503 | 1.8 % | 0 | 0.0 % | 640 | 0 | 1,122 |
| 4 | 3,368 s | 3,360 to 3,376 | 388 | 14 | 34,751 | 78,847 | 840,337 | 18,856,001 | - | - | 50,766 | 20,708 | 86 | 1.503 | 1.6 % | 0 | 0.0 % | 480 | 1 | 1,088 |
| 5 | 3,388 s | 3,380 to 3,396 | 388 | 14 | 34,755 | 78,844 | 812,959 | 20,336,819 | - | - | 48,941 | 24,380 | 86 | 1.503 | 1.5 % | 0 | 0.0 % | 520 | 3 | 1,105 |

### prestige-later-3

Rule: Chain, but the prestige waits until Factory Value is 3 times the threshold (or the run limit).

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | NW/min 900-1800 | C(n) | Deliveries | Reward/eff. | Contract share | Mined Ore | Mining share | Held off (machine-s) | Unspent IP | Commands |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 7,148 s | 7,140 to 7,156 | 748 | 17 | 3,844 | 11,156 | 95,687 | 1,331,076 | 4,302,316 | 10,169,882 | 5,635 | 10,108 | 242 | 1.501 | 3.5 % | 0 | 0.0 % | 1,480 | 3 | 1,809 |
| 2 | 4,128 s | 4,120 to 4,136 | 428 | 18 | 14,994 | 45,326 | 404,129 | 6,833,849 | 33,201,568 | - | 23,920 | 15,516 | 127 | 1.502 | 1.5 % | 0 | 0.0 % | 1,000 | 1 | 1,345 |
| 3 | 3,528 s | 3,520 to 3,536 | 388 | 19 | 30,576 | 72,006 | 598,398 | 13,270,163 | - | - | 35,092 | 20,044 | 111 | 1.503 | 1.2 % | 0 | 0.0 % | 920 | 4 | 1,282 |
| 4 | 3,192 s | 3,184 to 3,260 | 248 | 20 | 42,526 | 102,522 | 969,539 | 25,277,404 | - | - | 57,801 | 24,088 | 99 | 1.503 | 1.0 % | 0 | 0.0 % | 900 | 4 | 1,218 |
| 5 | 3,108 s | 3,100 to 3,116 | 248 | 21 | 47,291 | 105,161 | 985,255 | 32,685,537 | - | - | 58,672 | 28,000 | 95 | 1.503 | 0.9 % | 0 | 0.0 % | 940 | 7 | 1,215 |

### stop-at-bar

Rule: Miner and Smelter only; the Iron Bar is the terminal and is sold; feasible contracts only.

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | NW/min 900-1800 | C(n) | Deliveries | Reward/eff. | Contract share | Mined Ore | Mining share | Held off (machine-s) | Unspent IP | Commands |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 10,448 s | 10,440 to 10,456 | - | 10 | 7,389 | 17,180 | 79,162 | 602,332 | 1,751,250 | 3,297,689 | 4,132 | 10,508 | 104 | 1.500 | 1.1 % | 0 | 0.0 % | 0 | 2 | 1,112 |
| 2 | 6,608 s | 6,600 to 6,616 | - | 10 | 15,880 | 39,910 | 279,980 | 2,373,471 | 6,158,136 | - | 16,004 | 17,116 | 65 | 1.500 | 0.5 % | 0 | 0.0 % | 0 | 1 | 883 |
| 3 | 5,032 s | 5,024 to 5,080 | - | 11 | 21,302 | 64,569 | 542,205 | 4,147,965 | - | - | 31,842 | 22,156 | 50 | 1.500 | 0.4 % | 0 | 0.0 % | 0 | 2 | 828 |
| 4 | 4,572 s | 4,564 to 4,600 | - | 11 | 20,699 | 63,300 | 585,705 | 5,412,229 | - | - | 34,827 | 26,728 | 45 | 1.500 | 0.3 % | 0 | 0.0 % | 0 | 0 | 846 |
| 5 | 4,292 s | 4,284 to 4,340 | - | 12 | 22,379 | 68,127 | 722,885 | 7,279,281 | - | - | 43,650 | 31,020 | 42 | 1.500 | 0.3 % | 0 | 0.0 % | 0 | 4 | 851 |

### stop-at-plate

Rule: Miner, Smelter and Press only; the Iron Plate is the terminal and is sold; feasible contracts only.

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | NW/min 900-1800 | C(n) | Deliveries | Reward/eff. | Contract share | Mined Ore | Mining share | Held off (machine-s) | Unspent IP | Commands |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 9,028 s | 9,020 to 9,036 | - | 10 | 5,734 | 14,019 | 82,868 | 699,357 | 2,327,249 | 4,696,874 | 4,589 | 9,028 | 157 | 1.502 | 2.2 % | 0 | 0.0 % | 1,500 | 2 | 1,408 |
| 2 | 5,788 s | 5,780 to 5,796 | - | 10 | 15,315 | 46,393 | 339,089 | 2,696,180 | 8,786,388 | - | 19,513 | 14,816 | 105 | 1.503 | 1.3 % | 0 | 0.0 % | 680 | 1 | 1,122 |
| 3 | 4,688 s | 4,680 to 4,696 | - | 11 | 20,735 | 56,164 | 419,461 | 4,437,231 | - | - | 24,219 | 19,504 | 84 | 1.503 | 0.9 % | 0 | 0.0 % | 340 | 2 | 1,057 |
| 4 | 4,348 s | 4,300 to 4,356 | - | 11 | 22,173 | 60,682 | 545,088 | 6,054,663 | - | - | 32,293 | 23,852 | 78 | 1.504 | 0.8 % | 0 | 0.0 % | 420 | 0 | 1,044 |
| 5 | 4,228 s | 4,220 to 4,236 | - | 12 | 22,821 | 62,997 | 599,832 | 7,661,493 | - | - | 35,789 | 28,080 | 75 | 1.504 | 0.7 % | 0 | 0.0 % | 700 | 4 | 1,058 |

### market-only-chain

Rule: Chain with contracts off: no AcceptContract, no SetAutoDeliver, no Items from the ladder.

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | NW/min 900-1800 | C(n) | Deliveries | Reward/eff. | Contract share | Mined Ore | Mining share | Held off (machine-s) | Unspent IP | Commands |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 11,888 s | 11,880 to 11,896 | 1,048 | 10 | 1,864 | 5,789 | 44,736 | 463,700 | 1,552,642 | 3,088,228 | 2,596 | 11,888 | 0 | - | 0.0 % | 0 | 0.0 % | 0 | 2 | 799 |
| 2 | 6,968 s | 6,960 to 6,976 | 648 | 10 | 4,849 | 18,378 | 202,440 | 2,194,220 | 5,640,447 | - | 12,270 | 18,856 | 0 | - | 0.0 % | 0 | 0.0 % | 0 | 1 | 742 |
| 3 | 5,368 s | 5,300 to 5,376 | 488 | 11 | 9,810 | 35,001 | 446,738 | 4,074,405 | 12,259,201 | - | 27,449 | 24,264 | 0 | - | 0.0 % | 0 | 0.0 % | 0 | 2 | 750 |
| 4 | 4,588 s | 4,580 to 4,596 | 408 | 11 | 15,944 | 59,268 | 830,611 | 6,048,325 | - | - | 51,422 | 28,852 | 0 | - | 0.0 % | 0 | 0.0 % | 0 | 0 | 753 |
| 5 | 4,288 s | 4,280 to 4,296 | 388 | 12 | 16,792 | 59,041 | 934,246 | 7,697,407 | - | - | 58,347 | 33,140 | 0 | - | 0.0 % | 0 | 0.0 % | 0 | 4 | 757 |

### chain+mining

Rule: Chain, but the held click mines the whole per-second cap in every active second of every run.

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | NW/min 900-1800 | C(n) | Deliveries | Reward/eff. | Contract share | Mined Ore | Mining share | Held off (machine-s) | Unspent IP | Commands |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 6,988 s | 6,920 to 7,012 | 628 | 10 | 5,501 | 15,748 | 131,545 | 1,523,710 | 4,686,599 | - | 7,719 | 6,988 | 162 | 1.502 | 5.7 % | 5,552 | 0.1 % | 1,220 | 2 | 1,333 |
| 2 | 4,548 s | 4,540 to 4,556 | 348 | 10 | 21,972 | 57,121 | 414,314 | 5,389,994 | - | - | 23,812 | 11,536 | 109 | 1.503 | 3.3 % | 3,589 | 0.0 % | 720 | 1 | 1,118 |
| 3 | 3,776 s | 3,768 to 3,824 | 332 | 11 | 31,599 | 78,862 | 692,457 | 9,945,088 | - | - | 40,906 | 15,328 | 90 | 1.503 | 2.6 % | 2,973 | 0.0 % | 640 | 2 | 1,056 |
| 4 | 3,584 s | 3,500 to 3,616 | 372 | 11 | 33,437 | 81,143 | 679,669 | 13,359,475 | - | - | 39,830 | 18,944 | 84 | 1.503 | 2.3 % | 2,819 | 0.0 % | 520 | 0 | 1,045 |
| 5 | 3,528 s | 3,404 to 3,536 | 372 | 12 | 37,289 | 84,485 | 813,575 | - | - | - | 48,903 | 22,440 | 83 | 1.503 | 2.1 % | 2,769 | 0.0 % | 640 | 4 | 1,039 |

### mining-only

Rule: Builds nothing; mines the whole per-second cap in every active second; sells by auto-sell; buys Industry.

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | NW/min 900-1800 | C(n) | Deliveries | Reward/eff. | Contract share | Mined Ore | Mining share | Held off (machine-s) | Unspent IP | Commands |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | not reached | all not reached | - | - | 509 | 749 | 1,469 | 2,909 | 4,349 | 5,789 | 48 | - | 0 | - | 0.0 % | 17,280 | 100.0 % | 0 | - | 1 |
| 2 | not reached | all not reached | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - |
| 3 | not reached | all not reached | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - |
| 4 | not reached | all not reached | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - |
| 5 | not reached | all not reached | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - |

### contracts-only+sale

Rule: Report only: contracts-only plus one Sell of the whole Ore stock per run, at the first look at which a Miner stands; auto-sell stays off; no gate.

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | NW/min 900-1800 | C(n) | Deliveries | Reward/eff. | Contract share | Mined Ore | Mining share | Held off (machine-s) | Unspent IP | Commands |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | not reached | all not reached | 688 | - | 3,479 | 9,194 | 94,240 | 465,577 | 465,577 | 465,577 | 5,669 | - | 75 | 1.504 | 100.0 % | 0 | 0.0 % | 19,180 | - | 688 |
| 2 | not reached | all not reached | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - |
| 3 | not reached | all not reached | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - |
| 4 | not reached | all not reached | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - |
| 5 | not reached | all not reached | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - | - |

## 4. Gates (balance-policy-r2.md section 8.2)

From `contentRevision` 3 (`TraceGates.AssertsFromRevision`) every gate asserts, the content-tuned ones included (`ratio-content`, `item-uptime`, `chain-dominance`, `single-lever`); no `REPORTED` line remains. `ratio-bounds` is the always-asserted engine half of the former single ratio gate.

| Gate | Verdict | Status at revision 3 | Measured |
|---|---|---|---|
| `conservation` Conservation and exact per-command deltas | pass | asserted (passes) | 0 violations over 65 histories |
| `sale-neutrality` Sale value neutrality (FR-002) | pass | asserted (passes) | 30,461 checked Sells (12,395 lost 1 to the floor, 0 out of rule), 19 unchecked |
| `partition-invariance` Auto-deliver partition invariance (SC-003) | pass | asserted (passes) | identical state for 2 strategies, 44 contract completions compared |
| `remove-rebuild` Remove/rebuild (ECO-7) | pass | asserted (passes) | 10 machines replaced, largest change -1264 |
| `eco-3` Restated ECO-3 (FR-021) | pass | asserted (passes) | 7,148 s / 4,588 s / 3,808 s / 3,608 s / 3,408 s |
| `ratio-bounds` Contract ratio bounds, engine consistency (FR-004, D-D) | pass | asserted (passes) | 19,578 deliveries, effective ratio 1.498 to 1.800; 0 beyond the configured bound, 0 beyond the effective bound |
| `ratio-content` Contract ratio ranges, content-tuned (FR-004, D-D) | pass | asserted (passes) | 19,398 standing deliveries (highest configured ratio 1.5), 180 bonus deliveries (lowest configured ratio 1.8); 0 standing beyond 1.5, 0 bonus outside (1.5, 2.0] |
| `item-uptime` Item uptime (ECO-6, ladder.uptime) | pass | asserted (passes) | ladder.uptime at revision 3: holds; most Items granted in one run 10 (bound 10) |
| `start-affordability` Start affordability (FR-016, SC-011) | pass | asserted (passes) | reachable at every run start, smallest margin 65 Cash |
| `chain-dominance` Chain dominance per slot (ECO-12, chain.dominance) | pass | asserted (passes) | holds at revision 3 |
| `single-lever` No single-lever strategy faster than the chain (ECO-13) | pass | asserted (passes) | none faster (ore-only 2.44/2.30/2.07/1.91/1.81; stop-at-bar 1.46/1.44/1.32/1.27/1.26; stop-at-plate 1.26/1.26/1.23/1.21/1.24) |
| `contracts-only-longer` Contracts-only strictly longer than the chain (SC-002) | pass | asserted (passes) | run 1 not reached |
| `chain-within-6h` The chain reaches every threshold within 6 simulated hours | pass | asserted (passes) | slowest 7,156 s |
| `ip-4x` 4.0x rule: practical Industry ceiling over the highest IP (FR-021, ECO-9) | pass | asserted (passes) | ceiling 213 IP, highest IP 20 (prestige-later-3 run 4 offset 0), ratio 10.7 |
| `chain-plus-mining` Chain+mining not faster than the chain by more than miningAdvantageMaxPpm (MIN-9) | pass | asserted (passes) | advantage per run 2.2 % / 0.8 % / 0.8 % / 0.6 % / -3.5 % |

## 5. Orientation: ratios, warnings and reports (never gates)

Median time to eligibility relative to the chain (strategy median over chain median of the same run index; below 1.00x means faster than the chain):

| Strategy | Run 1 | Run 2 | Run 3 | Run 4 | Run 5 |
|---|---|---|---|---|---|
| `ore-only` | 2.44x | 2.30x | 2.07x | 1.91x | 1.81x |
| `contracts-only` | not reached | not reached | not reached | not reached | not reached |
| `idle-heavy` | 1.21x | 1.35x | 1.47x | 1.57x | 1.56x |
| `casual` | 1.68x | 2.26x | 2.60x | 2.77x | 2.95x |
| `prestige-later-1.5` | 1.00x | 0.97x | 0.94x | 0.93x | 0.99x |
| `prestige-later-3` | 1.00x | 0.90x | 0.93x | 0.88x | 0.91x |
| `stop-at-bar` | 1.46x | 1.44x | 1.32x | 1.27x | 1.26x |
| `stop-at-plate` | 1.26x | 1.26x | 1.23x | 1.21x | 1.24x |
| `market-only-chain` | 1.66x | 1.52x | 1.41x | 1.27x | 1.26x |
| `chain+mining` | 0.98x | 0.99x | 0.99x | 0.99x | 1.04x |
| `mining-only` | not reached | not reached | not reached | not reached | not reached |
| `contracts-only+sale` | not reached | not reached | not reached | not reached | not reached |

Cumulative play time C(n) to the prestige that completes run n (median over offsets) and the warning **W-later** (section 8.3):

| Strategy | C(1) | C(2) | C(3) | C(4) | C(5) |
|---|---|---|---|---|---|
| `chain` | 7,208 | 11,796 | 15,604 | 19,212 | 22,620 |
| `prestige-later-1.5` | 8,248 | 13,116 | 17,036 | 20,708 | 24,380 |
| `prestige-later-3` | 10,108 | 15,516 | 20,044 | 24,088 | 28,000 |

- W-later: not raised (waiting never beats prestiging at the threshold in cumulative time).
- W-market (P4a): not raised (contracts never cost the chain time against the market-only chain).
- W-mining (P4b): not raised. `mining-only` never reaches a threshold within 21,600 s (highest Factory Value read at a checkpoint 5,797); it mined 17,280 Ore per offset in run 1.
- P4c, contracts-only: run 1 not reached (Factory Value at 1,800 s: 1,037). It stalls by construction in run 1; the last checkpoint at which its Factory Value still changed is run second 1,800: auto-sell is off, the starting Cash buys one Miner, and every contract needs Bars, Plates or Gears. Its income is 100 % contracts by definition (0 Cash per minute). The stall itself is the SC-002 evidence: no bootstrap exception is applied to the gate (owner decision 2026-10-22, #406).
- Report only, `contracts-only+sale` (one `Sell` of the whole Ore stock at the first look at which a Miner stands, otherwise contracts-only; owner decision 2026-10-22): median time to eligibility by run not reached / not reached / not reached / not reached / not reached; Factory Value at 1,800 s of run 1: 94,240. It is not a gate and not an exception to SC-002 (`contracts-only-longer` reads `contracts-only` only).

Measured completions per run (the basis for `perRunCap` and the reachable bound of `contract.quantity.capacity`, #387 (a) and (d)); median of the chain over the offsets, and the highest count of any strategy, offset and run. The required units at the highest count are `quantity[count]` of the offer; the top storage capacity is 16,000.

| Offer | Chain run 1 | Run 2 | Run 3 | Run 4 | Run 5 | Highest in the matrix | Required units there |
|---|---|---|---|---|---|---|---|
| `contract.bonus-plate` | 1 | 1 | 1 | 1 | 1 | 1 | 100 |
| `contract.gear` | 59 | 40 | 34 | 31 | 28 | 89 | 319 |
| `contract.plate` | 43 | 29 | 24 | 22 | 21 | 63 | 237 |
| `contract.produce-plate` | 21 | 14 | 11 | 11 | 10 | 30 | 50 |
| `contract.timed-bar` | 60 | 40 | 34 | 33 | 29 | 104 | 379 |

Items granted per run (one Item per tier in the shipped content): chain median tier 1 / tier 2 / tier 3, and the highest of any run: run 1 3/3/3; run 2 3/3/2; run 3 3/3/2; run 4 3/2/2; run 5 3/2/2; highest 4/3/3. The ladder has 10 entries and `perRunCap` 10.
- Unspent IP and zero-gain levels (section 5): an affordable level whose fluid-model gain is not positive is never bought and is recorded (`industry_zero_gain.<id>`). None was recorded in this matrix. The model never values `industry.warehouse` (capacity has no effect in a model without a storage limit), which is the stated design. Medians of the unspent IP are in section 3.
- Mining income share, MIN-9 (sections 3 and 4): `chain+mining` holds the click at the whole cap in every active second; its advantage over the chain per run index is in the `chain-plus-mining` gate.
- Reward-to-effective-value ratio per delivery: see the `ratio-bounds` and `ratio-content` gates (observed range over every delivery in the matrix).

## 6. Runtime and the projection of the full matrix (PF-15), measured on policy r2

Wall-clock milliseconds of one history (runs 1 to 5, prestige and IP spending included) per strategy and offset, measured with `Stopwatch` around `BalanceTraceRunner.PlayRun` in a single-threaded Unity Edit Mode run on this workstation (`IndustryModel` memoised on its key; histories not parallelised). Load on the machine changes these numbers; the figures are an order of magnitude, not a benchmark.

| Strategy | Offset 1 | Offset 2 | Offset 3 | Offset 4 | Offset 5 | Strategy total | Run-plays | ms per run-play |
|---|---|---|---|---|---|---|---|---|
| chain | 6,078 ms (offset 0 s, 5 runs) | 1,082 ms (offset 4 s, 5 runs) | 1,111 ms (offset 8 s, 5 runs) | 1,121 ms (offset 12 s, 5 runs) | 1,082 ms (offset 16 s, 5 runs) | 10,474 ms | 25 | 419 |
| ore-only | 5,883 ms (offset 0 s, 5 runs) | 1,928 ms (offset 4 s, 5 runs) | 1,875 ms (offset 8 s, 5 runs) | 1,900 ms (offset 12 s, 5 runs) | 1,861 ms (offset 16 s, 5 runs) | 13,447 ms | 25 | 538 |
| contracts-only | 20 ms (offset 0 s, 1 runs) | 19 ms (offset 4 s, 1 runs) | 22 ms (offset 8 s, 1 runs) | 20 ms (offset 12 s, 1 runs) | 21 ms (offset 16 s, 1 runs) | 102 ms | 5 | 20 |
| idle-heavy | 6,126 ms (offset 0 s, 5 runs) | 1,222 ms (offset 4 s, 5 runs) | 1,215 ms (offset 8 s, 5 runs) | 1,194 ms (offset 12 s, 5 runs) | 1,182 ms (offset 16 s, 5 runs) | 10,939 ms | 25 | 438 |
| casual | 7,958 ms (offset 0 s, 5 runs) | 2,937 ms (offset 15 s, 5 runs) | 2,913 ms (offset 31 s, 5 runs) | 2,962 ms (offset 46 s, 5 runs) | 2,969 ms (offset 62 s, 5 runs) | 19,739 ms | 25 | 790 |
| prestige-later-1.5 | 2,827 ms (offset 0 s, 5 runs) | 1,190 ms (offset 4 s, 5 runs) | 1,170 ms (offset 8 s, 5 runs) | 1,207 ms (offset 12 s, 5 runs) | 1,170 ms (offset 16 s, 5 runs) | 7,564 ms | 25 | 303 |
| prestige-later-3 | 4,432 ms (offset 0 s, 5 runs) | 1,351 ms (offset 4 s, 5 runs) | 1,350 ms (offset 8 s, 5 runs) | 1,366 ms (offset 12 s, 5 runs) | 1,408 ms (offset 16 s, 5 runs) | 9,907 ms | 25 | 396 |
| stop-at-bar | 5,489 ms (offset 0 s, 5 runs) | 1,298 ms (offset 4 s, 5 runs) | 1,330 ms (offset 8 s, 5 runs) | 1,325 ms (offset 12 s, 5 runs) | 1,328 ms (offset 16 s, 5 runs) | 10,770 ms | 25 | 431 |
| stop-at-plate | 5,992 ms (offset 0 s, 5 runs) | 1,312 ms (offset 4 s, 5 runs) | 1,470 ms (offset 8 s, 5 runs) | 1,297 ms (offset 12 s, 5 runs) | 1,269 ms (offset 16 s, 5 runs) | 11,340 ms | 25 | 454 |
| market-only-chain | 1,351 ms (offset 0 s, 5 runs) | 1,367 ms (offset 4 s, 5 runs) | 1,398 ms (offset 8 s, 5 runs) | 1,369 ms (offset 12 s, 5 runs) | 1,381 ms (offset 16 s, 5 runs) | 6,866 ms | 25 | 275 |
| chain+mining | 6,597 ms (offset 0 s, 5 runs) | 1,710 ms (offset 4 s, 5 runs) | 1,718 ms (offset 8 s, 5 runs) | 1,725 ms (offset 12 s, 5 runs) | 1,704 ms (offset 16 s, 5 runs) | 13,454 ms | 25 | 538 |
| mining-only | 75 ms (offset 0 s, 1 runs) | 76 ms (offset 4 s, 1 runs) | 76 ms (offset 8 s, 1 runs) | 79 ms (offset 12 s, 1 runs) | 79 ms (offset 16 s, 1 runs) | 385 ms | 5 | 77 |
| contracts-only+sale | 210 ms (offset 0 s, 1 runs) | 197 ms (offset 4 s, 1 runs) | 197 ms (offset 8 s, 1 runs) | 211 ms (offset 12 s, 1 runs) | 199 ms (offset 16 s, 1 runs) | 1,014 ms | 5 | 203 |

Measured: 265 run-plays (13 strategies x 5 offsets x up to 5 runs) in 116.0 s of history time = 1.93 min, mean 438 ms per run-play, worst strategy `casual` 790 ms per run-play. Matrix wall time: 116.1 s. `IndustryModel`: 387 evaluations computed, 2,713 served from the memo.

| Projection of the full matrix (12 definitions x 5 offsets x 5 runs = 300 run-plays; sequential) | Mean-based | Worst-strategy-based | Against the 20 min budget |
|---|---|---|---|
| Projected from this run | 2.2 min | 3.9 min | within the budget |
| Ceiling: every run-play runs to the 21,600 s limit at the highest measured cost per simulated second (0.297 ms/s) | - | 32.1 min | over the budget |

Decision rule of `balance-policy-r2.md` section 8.1: if a projection exceeds the budget, memoise `T_fluid` and run histories in parallel first, then move only `mining-only` to an opt-in category; the offset count stays five and no FR-023 strategy is moved. The r1 projection of T311 (`strategy-baseline-r2.md` section 6) was 2.2 min mean and 3.5 min worst for the same 300 run-plays on five measured strategies.
