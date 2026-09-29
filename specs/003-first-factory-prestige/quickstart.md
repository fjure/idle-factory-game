# Quickstart Validation: First Iron Factory and Prestige Loop

**Prerequisites**:

- a clean checkout of the accepted commit;
- Unity `6000.6.0f1`;
- the repository runner (`docs/testing/unity-test-harness.md`).

Before any Unity run, acquire the leases. For the reason, see `AGENTS.md` › Shared workstation resources.

```powershell
scripts/resource-lease.ps1 -Acquire -Resource unity,emulator -Owner <owner> -Purpose "003 validation" -TtlMinutes 120
```

## 1. Content and rules without a scene (US6, FR-029/FR-030, SC-002)

```powershell
scripts/test-unity.ps1 -TestFile Assets/IdleFactory/Tests/EditMode/RunContentValidatorTests.cs
scripts/test-unity.ps1 -Suite EditMode
```

**Expected**:

- The canonical v2 document loads, and each rule code in [the content contract](contracts/gameplay-content-v2.md) has a rejection test.
- The simulator equivalence suite passes for single advances against partitions and permutations, including the FR-002 chain (62.5 % Press/Assembler) and the full-store case (US1-4).
- The IP table gives 10, 20, 30 and 100.
- Every command rejection code leaves the state unchanged.
- The Feature 001 and 002 legacy suites still pass.

## 2. Author a tuning change (US6)

1. Copy `Assets/IdleFactory/Content/Iron/iron-factory-content.json` to a scratch location.
2. Change one machine cost curve and one contract reward, and raise `contentRevision`.
3. Validate the copy with the authoring-check test and the scratch-path environment variable described in `docs/content/json-content-pipeline.md`.
4. Regenerate the balance trace for the scratch copy (§6) and compare it with the committed trace.

## 3. Play the loop (US1–US4, SC-001)

```powershell
scripts/test-unity.ps1                              # full Edit Mode + Play Mode
scripts/test-unity.ps1 -StandaloneWindowsPlayer     # tagged player suite: RunJourneyPlayerTests, RunTransitionPlayerTests, W003PlayerTimingTests
```

The manual editor walkthrough (keyboard only on desktop, then touch in the emulator, §8):

1. Build a Miner, Smelter, Press and Assembler in the four slots.
2. Fill the store until the Miner shows **Storage full** while the Smelter still completes.
3. Build a second Smelter, and reorder the priorities so that the lower slot wins.
4. Enable Bar auto-sell and observe the Press starve (the blocker is shown). Disable it again.
5. Accept the timed Bar contract and deliver exactly at the deadline, which succeeds. Accept again and let it expire: no reward, and the outcome card is shown.
6. Activate a Tier I Item on Miners. A second activation of the same effect is rejected, and the Item count is unchanged.
7. Buy Research with RP; the effect is immediate. Activate Overdrive and observe the countdown and then the cooldown.
8. Load the scripted pre-threshold fixture and open Prestige. It is disabled, and the missing FV is shown.
9. Load the eligible fixture, confirm, buy an Industry upgrade, and check that unused Items were kept while effects were reset.

## 4. Durable transitions (US4-3, US5, FR-024, FR-028)

The Edit Mode fault-injection suites (`PrestigeTransitionTests`, `SaveV2StoreTests` and `V1ImportTests`) and the player test `RunTransitionPlayerTests` cover every protocol step failing, followed by retry and by restart.

**Expected**:

- The result is either the old run with no award or the new run with exactly one award.
- No IP, Item or offline credit is duplicated.
- The V1 directory hash is identical before and after every import case.

## 5. Offline boundaries (US5-1, FR-025/FR-026)

`OfflineSegmentationTests` covers absences of 1 h, 8 h and 30 h, with an Item expiring, the Overdrive cooldown ending and a timed contract expiring inside the credited window and after the cap. It also covers a backward clock, a missing clock and a rotation without departure.

**Expected**:

- At most 6.4 h of effective production.
- Every effect applies over exactly `E(end) − E(start)`.
- The expired contract is visible with no reward.
- Rotation creates no summary.

## 6. Balance trace (SC-003, SC-004)

```powershell
scripts/test-unity.ps1 -TestFile Assets/IdleFactory/Tests/EditMode/Balance/BalanceTraceTests.cs
$env:IDLEFACTORY_REGENERATE_BALANCE_TRACE = "1"; scripts/test-unity.ps1 -TestFile Assets/IdleFactory/Tests/EditMode/Balance/BalanceTraceTests.cs; Remove-Item Env:IDLEFACTORY_REGENERATE_BALANCE_TRACE
```

**Expected**:

- The golden comparison passes.
- A regenerated trace has rows at 10, 15, 30, 60, 90 and 120 minutes, the first Gear time, the eligibility time, and the second-run block.
- The trace is summarized in `reviews/balance-trace-r<N>.md`.

The 2-hour and second-run claims stay "targets" until the named playtest record and the owner approval exist.

## 7. Performance W003 (SC-008)

Run `W003PerformanceTests` (Edit Mode allocations) and `W003PlayerTimingTests` (Windows player). Record in `reviews/final-acceptance.md`:

- per-frame simulation time at p95;
- the 8-hour offline reconcile median of 5;
- allocations, compared against the budgets in `plan.md` › Performance.

A miss triggers a Performance review.

## 8. Android emulator (FR-033, SC-006, SC-008; emulator only)

```powershell
scripts/android-emulator-journey.ps1 -Apk <f003-dev.apk> -LeaseOwner <owner>
```

The script's existing steps are `Preflight`, `Install`, `Launch`, `Rotation`, `Home`, `Lock`, `ColdRestart`, `LogScan` and `Profile`. Work item 21d adds the Feature 003 touch and rotation-during-confirmation checks as new steps; it does not change the existing ones.

Use an ARM64 Development build, cold boot the host-GPU AVD, and use a profiler-free build for the frame rate. Record the following:

- the touch journey;
- rotation during the Remove and Prestige confirmations: they are cancelled, nothing is spent, and no offline summary appears;
- the producing-factory FPS against the same-session control.

Label the results emulator-only. If the player cannot launch, record the blocker.

## 9. V1 import (US5-2/3, SC-005)

Put each committed V1 fixture into a temporary `v1/` root, then start the bootstrap in Play Mode with the store override.

**Expected**:

- The fitting fixtures import once with the mapped Ore, Bar and Smelters and proportional progress.
- `06-w001-scale` and the new over-capacity, over-slot and unsupported-ID fixtures show the failed rule and the OD-3 actions. V1 bytes stay unchanged.

Release the leases afterwards:

```powershell
scripts/resource-lease.ps1 -Release -Resource unity,emulator -Owner <owner>
```
