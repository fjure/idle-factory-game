# Quickstart: validating Feature 005

This guide gives the runnable validation scenarios. It covers prerequisites, commands and expected outcomes. Details live in the [contracts](contracts/) and the [data model](data-model.md).

## Prerequisites

- **Checkout**: a worktree of `feat/005-economy-contracts`, or a MAQA Issue worktree based on it. Unity `6000.6.0f1`.
- **Leases**: acquire them before any Unity or emulator run (AGENTS.md › Shared workstation resources):

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/resource-lease.ps1 -Acquire -Resource unity,emulator -Owner <issue-n|session> -Purpose "F005 validation" -TtlMinutes 120
```

## 1. Unit and integration suites

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity.ps1 -Suite EditMode
```

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity.ps1 -Suite PlayMode
```

Expected: all green with 0 skips. The F005 suites are:

- **Edit Mode**:
  - `PublicApiSnapshotTests` (#356);
  - `ReasonCodeContractTests` (#355);
  - `SalePricingTests`, `RunSellCommandTests`;
  - `ContractRewardsTests`, `AutoDeliverEquivalenceTests`, `ProduceObjectiveTests`, `ContractCooldownTests`, `BonusOfferTests`;
  - `BuildPricingTests`, `PrestigeThresholdTests`, `GoalsSnapshotTests`, `IndustryDiminishingTests`;
  - `RunContentV3*Tests`, `SaveV3*Tests`;
  - `LiveGapTests`, `SessionLogSchemaTests`;
  - `BalanceTraceTests` (fast gate).
- **Play Mode**:
  - `MarketSellPanelTests`, `ContractsAutoDeliverPanelTests`, `GoalsLinePanelTests`, `BuildPricePanelTests`, `SessionLogPanelTests`;
  - layout tests at every screen class at 100 % and 200 % text;
  - `RunDesktopLifecycleTests` (+ #354 cases).

## 2. Full balance matrix (per batch and before content approval)

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity.ps1 -Suite EditMode -Category BalanceFull
```

`-Category` requires `-Suite` (or `-TestFile`) and cannot be combined with `-StandaloneWindowsPlayer` (T310).

The strategy matrix and the r2 strategy baseline report (T311) run only on opt-in:

```bash
powershell -NoProfile -Command "$env:IDLEFACTORY_WRITE_STRATEGY_BASELINE='1'; & ./scripts/test-unity.ps1 -Suite EditMode -Category BalanceStrategies"
```

Expected:
- `reviews/balance-trace-r3.md` and `Fixtures/Balance/trace-r3.csv` are regenerated only on opt-in.
- The FR-024 gates pass.
- SC-002 and SC-004 hold on medians over 5 offsets.
- The Ore-only and contracts-only rows exist for runs 1–5 (#357).

## 3. Windows test player (once per batch)

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity.ps1 -StandaloneWindowsPlayer
```

Expected:
- The journey selling → contract with auto-deliver → prestige with the rising threshold → next-run build price passes.
- The `W005-L` 8 h return takes ≤ 500 ms (median of 5).
- The acceleration p95 is ≤ 1 ms at S_max in a development player.
- A forced log write failure leaves saving unaffected.

## 4. Release build absence check

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-release-build.ps1 -BuildPath <non-development build>
```

Expected:
- No `IdleFactory.DevTools` assembly or type names in the build.
- For an Android build, the merged manifest excludes `SessionLog/` and `Exports/` from backup.

## 5. Android emulator (emulator-only evidence)

```bash
powershell -NoProfile -Command "& ./scripts/android-emulator-journey.ps1 -Apk <gles3.apk> -LeaseOwner <owner>"
```

Expected:
- The F005 steps pass: sell, auto-deliver toggle, session-log export (path shown; the file is retrieved with `adb pull`, D-M).
- The `W005-L` return takes ≤ 2 s, and play holds 60 fps.

## 6. Human and agent gates

- **#353**: the W003-B Option 0 record (`reviews/w003b-measurement.md`) exists before the selling and auto-sell work unit.
- **Play-Tester**: a report per affected batch (`reviews/playtest-agent-*.md`), reviewed by the owner (SC-010).
- **Accelerated sessions and offline return**: after a DevTools-accelerated session the game timeline runs ahead of the device clock, so every later start reports a backward clock and credits no offline production. Test offline return and live gaps on a save that was never accelerated (batch-B F-B5, owner decision 2026-10-22).
- **Human playtest**: a named player reaches the first prestige and a second run (`reviews/playtest-*.md`, SC-005). It includes the exported session log and the 1–5 answer.
- **Content approval**: the owner approves `contentRevision` 3 (FR-027).

Release the leases afterwards:

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/resource-lease.ps1 -Release -Resource unity,emulator -Owner <issue-n|session>
```

## Leases per step (OPS-7)

| Activity | Leases |
|---|---|
| Edit Mode or Play Mode run (`test-unity.ps1 -Suite`) | `unity` (`unity,emulator` if the worktree's `Library/` came from an Android build) |
| Windows test player (`-StandaloneWindowsPlayer`) or development-player timing run | `unity,emulator` |
| Android development, GLES3 or release build; step 12c | `unity,emulator` |
| Android emulator journey | `emulator` (the script acquires it) |
| `check-release-build.ps1` on a finished build | none |
