# Strategy baseline on content revision 2 (T311, issue #373)

**Orientation only. No band in this report is asserted.** The gates are the FR-024 list of `balance-policy-r2.md` section 8.2, which T341 adds. Every number below is a measurement of the engine on the content named here, by the generator `StrategyBaselineReport` (`Assets/IdleFactory/Tests/EditMode/Balance/`), run through `StrategyFrameworkTests.WriteStrategyBaselineReport` (category `BalanceStrategies`, opt-in write with `IDLEFACTORY_WRITE_STRATEGY_BASELINE=1`).

## 1. Inputs

| Item | Value | Source |
|---|---|---|
| Build commit | `ad0b5bf56` | `git rev-parse` in the generator |
| Content | `Assets/IdleFactory/Content/Iron/iron-factory-content.json`, `contentRevision` 2 | loaded by `GameplayContentDispatcher.Load` |
| Content state | **Transitional**: schema version 3 with the revision 2 values; contract Cash comes through the transitional reward adapter (`ceil(quantity x price x ratio)`), not the F003 curves | `Assets/IdleFactory/Tests/EditMode/Fixtures/Balance/trace-r2-transitional.md` (T313) |
| Policy | the current reference policy (policy r1 behaviour) behind `IStrategyPolicy`; policy r2 is T341 | `ReferencePolicy.cs` |
| Look grid | looks at run second `offset + k x L`; reference `L` = 20 s; away window 240 to 299 of every 300 s (idle-heavy: 120 to 299) | `balance-policy-r2.md` sections 2, 7 |
| Offsets | 0, 4, 8, 12, 16 s (casual: `i x L_casual / 5`, integer division: 0, 14, 29, 43, 58 s) | `balance-policy-r2.md` section 8.1 |
| Median | per run index over the 5 offsets: third smallest, "not reached" ordered above every number; a `-` means fewer than 3 offsets have the value (for example the run ended before that second) | `StrategyEnsemble.Median` |
| Time to threshold | run second of the first look at which the prestige preview reports eligible (`EligibleAtSec`); limit 21,600 s of run time | `BalanceTraceRunner.PlayRun` |
| Not simulated | the held mining click (no `Mine` command before F005): `casual` uses its look interval only, no 25 % mining | `balance-policy-r2.md` sections 6, 7 |

## 2. Casual calibration (section 7.1)

`ReferencePolicy.CasualLookIntervalSeconds` = **73 s** [pinned]. The search on this build returns 73 s (equal to the pin).

- Rule: the smallest integer `L`, searched from 40 s in steps of 1 s, for which the median over the five casual offsets of the Factory Value at run second 900 of run 1 is at most 0.4 (1 / 2.5, the owner playtest of 2026-10-09, `docs/agents/roles-and-reviews.md` Play-Tester) of the chain median at the same second.
- Chain median Factory Value at second 900 (offsets 0, 4, 8, 12, 16): **12,874**.
- Casual median at `L` = 73 s: **4,701**; ratio **0.365** (limit 0.400).
- Candidates tried (L: casual median FV at 900 s): 40 s: 11032; 41 s: 11141; 42 s: 11212; 43 s: 10871; 44 s: 10429; 45 s: 10822; 46 s: 10478; 47 s: 10808; 48 s: 10274; 49 s: 10453; 50 s: 10828; 51 s: 10564; 52 s: 8350; 53 s: 9067; 54 s: 8090; 55 s: 8316; 56 s: 8786; 57 s: 8834; 58 s: 8290; 59 s: 7968; 60 s: 8071; 61 s: 7935; 62 s: 7670; 63 s: 7773; 64 s: 7733; 65 s: 7504; 66 s: 7510; 67 s: 7229; 68 s: 6650; 69 s: 6836; 70 s: 6018; 71 s: 5839; 72 s: 5560; 73 s: 4701.
- The Factory Value is read at the exact run second 900, by splitting the look step that spans it into two advances. Source: `BalanceTraceRunner.RecordCheckpoints`.
- Caveat for the pin rule: section 7.1 specifies policy r2 including the 25 % held mining click. Neither exists before T341/F005, so this constant was found for the current reference policy without mining. It is pinned as the section says (never recalibrated); the ratio drifts with later changes and is reported as orientation.

## 3. Medians per strategy and run index

Columns: time to eligibility (median, min to max over the five offsets); first Gear (median); IP awarded at the prestige (median); Factory Value at run second 600, 900, 1,800, 3,600, 5,400, 7,200 (median); contracts delivered, commands and store-blocked machine samples (medians).

### chain

Rule: Reference policy: full chain, contracts, prestige at the first non-idle look at or after eligibility.

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | Contracts | Commands | Store-blocked |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 6,548 s | 6,540 to 6,556 | 728 | 10 | 4,620 | 12,874 | 109,317 | 1,306,306 | 4,740,368 | - | 189 | 2,295 | 417 |
| 2 | 4,648 s | 4,640 to 4,656 | 328 | 10 | 28,043 | 76,933 | 719,152 | 5,374,068 | - | - | 146 | 4,240 | 237 |
| 3 | 4,648 s | 4,640 to 4,656 | 328 | 10 | 28,043 | 76,933 | 719,152 | 5,374,068 | - | - | 146 | 6,181 | 237 |
| 4 | 4,328 s | 4,320 to 4,336 | 168 | 10 | 40,566 | 129,309 | 1,149,423 | 6,783,689 | - | - | 134 | 8,012 | 352 |
| 5 | 4,328 s | 4,320 to 4,336 | 168 | 10 | 40,566 | 129,309 | 1,149,423 | 6,783,689 | - | - | 134 | 9,843 | 352 |

### ore-only

Rule: Only Miners are built; no contracts; all Ore sold by auto-sell; prestige at the threshold.

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | Contracts | Commands | Store-blocked |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 6,028 s | 6,020 to 6,036 | - | 10 | 4,675 | 9,541 | 68,525 | 1,101,959 | 5,884,913 | - | 0 | 670 | 0 |
| 2 | 2,968 s | 2,960 to 2,976 | - | 10 | 13,961 | 36,382 | 596,201 | - | - | - | 0 | 1,335 | 0 |
| 3 | 2,968 s | 2,960 to 2,976 | - | 10 | 13,961 | 36,382 | 596,201 | - | - | - | 0 | 1,996 | 0 |
| 4 | 2,328 s | 2,320 to 2,336 | - | 10 | 31,880 | 87,667 | 1,702,795 | - | - | - | 0 | 2,696 | 0 |
| 5 | 2,328 s | 2,320 to 2,336 | - | 10 | 31,880 | 87,667 | 1,702,795 | - | - | - | 0 | 3,396 | 0 |

### contracts-only

Rule: Chain machine mix; auto-sell off for every resource, the only Cash from the factory is contract rewards.

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | Contracts | Commands | Store-blocked |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | not reached | all not reached | - | - | 629 | 929 | 1,037 | 1,037 | 1,037 | 1,037 | 0 | 1 | 1,031 |
| 2 | not reached | all not reached | - | - | - | - | - | - | - | - | - | - | - |
| 3 | not reached | all not reached | - | - | - | - | - | - | - | - | - | - | - |
| 4 | not reached | all not reached | - | - | - | - | - | - | - | - | - | - | - |
| 5 | not reached | all not reached | - | - | - | - | - | - | - | - | - | - | - |

### idle-heavy

Rule: Chain, but the player is away from second 120 to 299 of every 300 s (60 % idle).

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | Contracts | Commands | Store-blocked |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 11,208 s | 11,200 to 11,216 | 928 | 10 | 4,062 | 8,536 | 36,967 | 332,376 | 1,137,154 | 3,000,741 | 174 | 2,185 | 831 |
| 2 | 9,108 s | 9,100 to 9,116 | 388 | 10 | 9,473 | 21,918 | 134,450 | 1,095,174 | 2,858,378 | 5,795,168 | 149 | 4,136 | 837 |
| 3 | 9,108 s | 9,100 to 9,116 | 388 | 10 | 9,473 | 21,918 | 134,450 | 1,095,174 | 2,858,378 | 5,795,168 | 149 | 6,083 | 837 |
| 4 | 9,048 s | 9,040 to 9,056 | 628 | 10 | 16,370 | 35,604 | 197,844 | 1,375,154 | 3,335,546 | 6,302,129 | 146 | 8,032 | 1,252 |
| 5 | 9,048 s | 9,040 to 9,056 | 628 | 10 | 16,370 | 35,604 | 197,844 | 1,375,154 | 3,335,546 | 6,302,129 | 146 | 9,981 | 1,252 |

### casual

Rule: Chain with a look every 73 s (pinned L_casual); no held mining click yet (needs the F005 Mine command).

| Run | Eligible median | min to max | First Gear | IP | FV@600 | FV@900 | FV@1800 | FV@3600 | FV@5400 | FV@7200 | Contracts | Commands | Store-blocked |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 17,520 s | 16,979 to 18,410 | 905 | 10 | 2,769 | 4,701 | 22,682 | 150,677 | 512,566 | 1,144,858 | 157 | 2,043 | 348 |
| 2 | not reached | 20,381 to not reached | 13,621 | - | 6,794 | 9,546 | 15,990 | 30,625 | 50,645 | 78,979 | 233 | 3,588 | 837 |
| 3 | not reached | 20,381 to not reached | - | - | - | - | - | - | - | - | - | - | - |
| 4 | not reached | 13,446 to not reached | - | - | - | - | - | - | - | - | - | - | - |
| 5 | not reached | 13,446 to not reached | - | - | - | - | - | - | - | - | - | - | - |

## 4. Observations (derived from section 3; no verdicts)

- `ore-only`: median time to eligibility relative to the chain, run 1: 0.92x, run 2: 0.64x, run 3: 0.64x, run 4: 0.54x, run 5: 0.54x.
- `contracts-only`: no run reached the threshold; not reached (median) at run 1, 2, 3, 4, 5.
- `idle-heavy`: median time to eligibility relative to the chain, run 1: 1.71x, run 2: 1.96x, run 3: 1.96x, run 4: 2.09x, run 5: 2.09x.
- `casual`: median time to eligibility relative to the chain, run 1: 2.68x; not reached (median) at run 2, 3, 4, 5.
- `contracts-only` run 1 stalls: its median commands are 1 and its store-blocked samples 1,031 (section 3), and the Factory Value stays flat from the last checkpoints. With auto-sell off the first Miner only fills the store and no Cash arrives to buy a machine or storage; the existing contracts need Bars or Plates. This is the by-construction stall of balance-policy-r2.md section 8.3 (P4c), reported and not a defect of the harness.
- Each ratio divides a strategy median by the chain median of the same run index (section 3). A ratio below 1.00x means the strategy reached the threshold earlier than the chain on this content and policy; it is reported, not judged (the single-lever gate is T341's, on policy r2).
- A run that is not reached ends the history: the later run indices of that strategy cannot be played and show `-` in section 3.

## 5. Conservation

Conservation violations recorded by the trace commands across all 25 histories: **0** (`TraceContext.Violations`).

## 6. Runtime and the 9-strategy projection (PF-15)

Wall-clock milliseconds of one history (runs 1 to 5, prestige and IP spending included) per strategy and offset, measured with `Stopwatch` around `BalanceTraceRunner.PlayRun` in a single-threaded Unity Edit Mode run on this workstation. Load on the machine changes these numbers; the figures are an order of magnitude, not a benchmark.

| Strategy | Offset 1 | Offset 2 | Offset 3 | Offset 4 | Offset 5 | Strategy total | Run-plays | ms per run-play |
|---|---|---|---|---|---|---|---|---|
| chain | 2,088 ms (offset 0 s, 5 runs) | 2,093 ms (offset 4 s, 5 runs) | 2,093 ms (offset 8 s, 5 runs) | 2,090 ms (offset 12 s, 5 runs) | 2,108 ms (offset 16 s, 5 runs) | 10,472 ms | 25 | 419 |
| ore-only | 1,554 ms (offset 0 s, 5 runs) | 1,551 ms (offset 4 s, 5 runs) | 1,566 ms (offset 8 s, 5 runs) | 1,561 ms (offset 12 s, 5 runs) | 1,558 ms (offset 16 s, 5 runs) | 7,790 ms | 25 | 312 |
| contracts-only | 12 ms (offset 0 s, 1 runs) | 12 ms (offset 4 s, 1 runs) | 12 ms (offset 8 s, 1 runs) | 12 ms (offset 12 s, 1 runs) | 17 ms (offset 16 s, 1 runs) | 65 ms | 5 | 13 |
| idle-heavy | 2,302 ms (offset 0 s, 5 runs) | 2,293 ms (offset 4 s, 5 runs) | 2,301 ms (offset 8 s, 5 runs) | 2,309 ms (offset 12 s, 5 runs) | 2,310 ms (offset 16 s, 5 runs) | 11,515 ms | 25 | 461 |
| casual | 3,588 ms (offset 0 s, 5 runs) | 3,519 ms (offset 14 s, 5 runs) | 1,412 ms (offset 29 s, 2 runs) | 1,289 ms (offset 43 s, 2 runs) | 1,315 ms (offset 58 s, 2 runs) | 11,123 ms | 16 | 695 |

Measured: 96 run-plays (5 strategies x 5 offsets x up to 5 runs) in 41.0 s = 0.68 min, mean 427 ms per run-play, worst strategy 695 ms per run-play.

| Projection (sequential, no memoisation, no parallelism) | Run-plays | Mean-based | Worst-strategy-based | Against the 20 min budget |
|---|---|---|---|---|
| 9 FR-023 strategies x 5 offsets x 5 runs | 225 | 1.6 min | 2.6 min | within the budget |
| 12 strategy definitions of section 8.1 x 5 offsets x 5 runs | 300 | 2.1 min | 3.5 min | within the budget |
| Ceiling: every run-play runs to the 21,600 s limit at the highest measured cost per simulated second (0.094 ms/s), 9 / 12 strategies | 225 / 300 | - | 7.6 / 10.1 min | within the budget |

Method: projected minutes = run-plays x measured ms per run-play / 60,000. The mean is over the five strategies measured here; the worst-strategy figure applies the slowest strategy's cost to every run-play. A strategy that never reaches the threshold plays to the 21,600 s limit and costs more than one that prestiges, so the projection leans on whether the unmeasured strategies (stop-at-bar, stop-at-plate, prestige-later, market-only chain, chain+mining, mining-only) look like the measured ones. Decision rule of `balance-policy-r2.md` section 8.1: if a projection exceeds the budget, memoise `T_fluid` and run histories in parallel first, then move only `mining-only` to an opt-in category; the offset count stays five.
