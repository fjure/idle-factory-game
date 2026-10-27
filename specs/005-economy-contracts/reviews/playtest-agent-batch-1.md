# Play-Tester batch-1 report (T357, issue #378, WU17)

**Status: proposal for the owner.** This is a headless, deterministic measurement by the balance runner. It is not a human playtest, approves no content revision and sets no band. Every finding is open for owner decision (AGENTS.md; `docs/agents/roles-and-reviews.md` Play-Tester).

| Item | Value | Source |
|---|---|---|
| Build commit | `53ceb5407` (worktree HEAD `53ceb54`, base `main`) | `git rev-parse HEAD`; generator header |
| `contentRevision` | 2, transitional (schema v3, revision 2 values, contract Cash through `RunRewardAdapter`) | `Assets/IdleFactory/Tests/EditMode/Fixtures/Balance/trace-r2-transitional.md` |
| Strategies | chain, ore-only, contracts-only, idle-heavy, casual | `specs/005-economy-contracts/reviews/strategy-baseline-r2.md` section 3 |
| Channel | balance runner (`StrategyFrameworkTests`, category `BalanceStrategies`); no player build, no emulator | this report |
| Simulated time | runs 1 to 5 per history, 5 look offsets per strategy, 21,600 s limit per run | `strategy-baseline-r2.md` section 1 |
| Policy | reference policy r1 behaviour behind `IStrategyPolicy`; policy r2 is T341 | `strategy-baseline-r2.md` section 1 |

## 1. Run and determinism

- Command run from the worktree (the main checkout has an older runner and is on `main`): `IDLEFACTORY_WRITE_STRATEGY_BASELINE=1 powershell -NoProfile -Command "& ./scripts/test-unity.ps1 -Suite EditMode -Category BalanceStrategies"`. Exit code 0; `UNITY_TEST_PASS suite=EditMode tests=10 passed=10 skipped=0`. Wall time 42.2 s for 96 run-plays.
- Determinism: **equal**. Every section 3 median, section 4 observation and section 5 conservation value of the regenerated file matches the committed `strategy-baseline-r2.md`. The diff is limited to the build-commit label (`4ae6a7a37` committed, `53ceb5407` regenerated, so the committed header is stale, not the numbers) and the section 6 wall-clock timings (machine load).
- The regenerated file was restored with `git checkout`; `ProjectSettings` noise was discarded. Nothing from the run is committed except this report.

## 2. Deviations per strategy (medians over 5 offsets, source `strategy-baseline-r2.md` section 3)

| Strategy | Observed (run 1 unless noted) | Reference | Reading |
|---|---|---|---|
| chain | eligibility 6,548 s (109.1 min; range 6,540 to 6,556); first Gear 728 s; run 2 at 4,648 s; runs 1 to 5: 6,548 / 4,648 / 4,648 / 4,328 / 4,328 s | ECO-2: run 1 eligibility 6,600 to 7,800 s; first Gear 600 to 900 s; run 2 4,200 to 4,800 s. F003 r2 record: 6,940 / 4,640 s | Run 1 is 52 s below the ECO-2 band. First Gear and run 2 are inside. |
| ore-only | eligibility 6,028 s (0.92x chain); runs 2 to 5 at 0.64x, 0.64x, 0.54x, 0.54x of chain | Single-lever gate (`balance-policy-r2.md` section 8.2, FR-024): no single-lever strategy faster than the chain | Ore-only is faster than the chain in every run index. Dominance is reduced from the F003 probe but not removed. |
| contracts-only | no run reaches eligibility (run 1 median "not reached"); Factory Value flat at 1,037 from 1,800 s; 0 contracts; 1,031 store-blocked samples | `balance-policy-r2.md` section 8.3 (P4c) | Stall by construction with auto-sell off: the Miner fills the store, no Cash arrives, and the contracts need Bars or Plates. Reproduces the expected deadlock. |
| idle-heavy | eligibility 11,208 s (1.71x chain); run 5 at 9,048 s (2.09x) | no F003 band (reported only) | Slower as designed (60 % away). Informational. |
| casual | L = 73 s (provisional); run 1 eligibility 17,520 s (2.68x chain; range 16,979 to 18,410); run 2 not reached (min 20,381 s); FV at 900 s ratio 0.365 | Casual FV ratio limit 0.40 (`balance-policy-r2.md` section 7.1) | Run 2 is slower than run 1 within the same history. Suspected artifact of policy r1: no held mining click (25 % not simulated). |

Values from `strategy-baseline-r2.md` section 3, which this run reproduced exactly.

## 3. Changed reward outcomes from the transitional adapter

Source: `trace-r2-transitional.md`. Contract Cash grows faster from completion index 2:

| Offer | Cash at index 2 (F003 to adapter) | Cash at index 5 (F003 to adapter) |
|---|---|---|
| Plate | 1,429 to 1,442 | 1,474 to 1,526 |
| Gear | 4,081 to 4,120 | 4,206 to 4,360 |
| timed Bar | 522 to 526 | 540 to 556 |

Effect on pacing (median against F003 r2 record):
- Run 1 eligibility: 6,940 s to 6,548 s (about 392 s earlier). This moves it from inside the ECO-2 band to 52 s below it.
- Runs 2 to 5: 4,640 / 4,640 / 4,320 / 4,320 s to 4,648 / 4,648 / 4,328 / 4,328 s (about 8 s later, within the bands).
- First Gear unchanged (720 s F003, 728 s median here; the adapter does not touch indices 0 and 1).
- The F003 SC-003 "near 120 minutes" window (110 to 130 min) is missed by run 1 at 109.1 min. This is the same measured effect; the SC-003 target stays a target until the owner approves.
- The trace notes say T320 replaces the adapter and moves contract Cash from ceil to floor, so these values will move again.

## 4. Findings

| ID | Observed behaviour | Evidence | Affected requirement | Severity | Proposed disposition |
|---|---|---|---|---|---|
| F1 | Chain run 1 eligibility 6,548 s, below the ECO-2 band (6,600 to 7,800 s) after the adapter | section 2 and 3 above; `trace-r2-transitional.md` | F003 ECO-2, SC-003 | Medium | Named later task: T320 (reward formula), then T341 to restore the asserted bands. Owner decides whether the band stays. |
| F2 | Ore-only reaches eligibility earlier than the chain in every run index (0.92x to 0.54x) | `strategy-baseline-r2.md` section 3 and 4 | `balance-policy-r2.md` section 8.2 single-lever gate; SC-002 | High | Named later tasks: T341 (gate on policy r2), T345 (tuning). Owner decides whether the gate fails this content. |
| F3 | Contracts-only deadlocks with auto-sell off (no run reaches eligibility) | section 2 (P4c) | `balance-policy-r2.md` section 8.3 (orientation) | Low (known by construction) | Owner question 1 below. No defect proposed. |
| F4 | Casual run 2 is slower than run 1 within one history; casual L = 73 s is provisional and no mining click is simulated | `strategy-baseline-r2.md` section 2 and 4 | `balance-policy-r2.md` section 7.1, FR-023 | Medium (reporting only) | Named later task: T341 (diagnose, final pin). Not a finding about the game until diagnosed. |
| F5 | Idle-heavy takes 1.7x to 2.1x as long as the chain | section 2 | no band (reported) | Idea | No action; informational. |

Feel: no feel statements are made here. Any reading that pacing feels slow or fast would be the owner's inference, not a measurement.

## 5. Open owner questions

1. Contracts-only stall: accept the by-construction bootstrap stall (P4c), or ask for a bootstrap rule in content? (F3)
2. Run-1 eligibility at 6,548 s is 52 s below the ECO-2 band after the adapter. Keep the band and wait for T320 and T341, or amend the band? (F1)
3. Ore-only beats the chain in every run index. Is this a single-lever gate failure under `balance-policy-r2.md` section 8.2 for this content, or accepted for r2? (F2)
4. Final casual pin in T341: the current 73 s is provisional and is pinned on the r1 policy without mining. Confirm T341 owns the final pin. (F4)
5. ProduceMinQuantity=10: not measured in this batch. What is the owner's position before T341? (out of scope of this run)
6. cost.floor=0.5x: not measured in this batch. What is the owner's position before T341? (out of scope of this run)
