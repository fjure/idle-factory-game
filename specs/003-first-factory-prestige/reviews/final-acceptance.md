# Feature 003: Final acceptance (T133)

**Task:** T133 (parent task, work unit WU70), after T130, T131, T132 and T251. Covers SC-001 to SC-008.
**Accepted commit:** `ac4256c` (origin/main, merge of PR #351, T251). Every run of record below used a clean worktree of this commit (`.maqa-worktrees/t133-final-acceptance`).
**History:** a first T133 run on `8a8e518` (merge of PR #348) found the Android Back regression described in section 5.1. The owner decided to fix it before acceptance (T251, Issue #350, PR #351), and T133 was rerun in full on `ac4256c`. The first run's evidence is kept as history.
**Date:** 2026-10-09 to 2026-10-10. **Parent:** Claude Code orchestrator (High tier). **Leases:** `unity` and `emulator`, owner `t133-final-acceptance`, held for every Unity and emulator step and released afterwards.
**Host:** Windows 11 Pro 10.0.26200, AMD Ryzen 7 7800X3D (8 cores), 31.8 GB RAM, Unity `6000.6.0f1`. Emulator `emulator-5554`, AVD `Medium_Phone_API_36.0` (API 36, x86_64 with ARM64 translation, host GPU), cold-booted for this task.

## 1. Verdict

**ACCEPTED WITH RECORDED DEVIATIONS.** The Android Back regression found by the first run is fixed (T251) and proven on the emulator. Every suite is green on the accepted commit, the console holds no relevant error, and the settings-noise check passes. Every FR and SC has evidence. SC-008 is **met for the shipped workloads, with the W003-B miss accepted by the owner as a tracked risk** (section 4), never reported as plainly passing. SC-003 and SC-004 are **scripted-trace targets**, not observed human times. Android figures are **emulator-only**; physical Android and iOS are not claimed (iOS deferred, Issue #39).

## 2. Owner decisions recorded for T133 (2026-10-09)

The owner answered the open decisions of T130, T131 and T132 in the T133 session before the runs.

| # | Item | Source | Decision |
|---|---|---|---|
| D1 | SC-008 / PR-1: W003-B 8 h reconcile misses 500 ms | `performance-review.md` 4.1, 7 | **Option C:** accept the miss for Feature 003 as a recorded deviation, with option A (measure first: quiet host, IL2CPP release player, W003-B on the emulator, CPU attribution) as a follow-up Issue. Whether and how to optimize is decided on that measurement (Architect position: Option 1 acceptable as an additional index-based API; Option 3 only after an Architect-reviewed design). SC-008 reads "met for the shipped workloads, W003-B miss accepted". |
| D2 | PR-2: the HUD read path allocates once per simulated second (Overdrive status object) | `performance-review.md` 4.2 | **Accepted.** Budget 1 is read as "advance path allocation-free; event-driven HUD allocations at most once per second allowed". Revisited in the performance follow-up; no fix now. |
| D3 | F-9: world slots below 44 pt (fit-all minimum 29.8 pt; zoom minimum 32 pt) | `ui-ux-review.md`, `feature-review.md` | **Exemption for Feature 003.** The slot is a world object; its state and every action are repeated as text and as 44 pt controls in the panels, and the shell starts at fit-all. F004 (UI/UX and graphics) takes over the touch-target sizing. FR-032 and SC-007 hold for every actionable UI control. |
| D4 | N-5: cold start after a short unclean exit shows the Welcome back summary at any length | `ui-ux-review.md` | **Keep** the current behaviour (the credit is real; restarts are rare). Brevity is handled in the F004 offline popup rework. |
| D5 | F-3: no timer autosave while the desktop window is unfocused | `security-devops-review.md` | **Confirmed** as intended: one safety checkpoint on focus loss (T250), no autosave while unfocused. The owner acknowledges the residual risk (a crash after a long unfocused session loses progress after the focus-loss checkpoint). |
| D6 | Item f / N-9: the blocker section sits below the fold at 200 % text on small screens | `ui-ux-review.md` | **Leave for now; F004.** Known limitation; the status line near the top already names the cause. |
| D7 | Art: AD-1 (world background `#090C0E` against `#111827`), AD-4 (text-only buttons, 27 of 49 icons and the tier chips unused), AD-6 / N1 (Iron Ore icon 2.05:1 on the raised card) | `art-direction-review.md` | **Deferred to F004** as accepted deviations of the provisional Feature 003 art. |
| D8 | T133 first run: Android Back on an open confirmation closed the app (regression from the T250 `runInBackground` setting) | section 5.1 | **Fix before acceptance** (T251, Issue #350). Then, to close the independent QA warning W1, **add a StandalonePlayer test** that observes the desktop `runInBackground` initializer in the built Windows player before the merge. |

## 3. Suite, console and settings evidence on `ac4256c`

| Run | Command | Result | Raw result (local, ignored) |
|---|---|---|---|
| Edit Mode (full) | `scripts/test-unity.ps1` | **PASS, 1522 of 1522, 0 skipped** | `test-results/unity/20261009T221655979Z/EditMode-results.xml` |
| Play Mode (full) | same invocation | **PASS, 1530 of 1530, 0 skipped** | `.../20261009T221655979Z/PlayMode-results.xml` |
| Windows test player (tagged `StandalonePlayer`) | `scripts/test-unity.ps1 -StandaloneWindowsPlayer` | **PASS, 24 of 24, 0 skipped**; no `PlayerWithTests.exe` left running | `.../20261009T222542402Z/PlayerPlayMode-results.xml` |
| Settings noise | `scripts/check-settings-noise.ps1 -Commit ac4256c` | **OK** | console |

First run on `8a8e518` (history): Edit 1519 of 1519, Play 1529 of 1529, Player 23 of 23, settings noise OK (`20261009T195051056Z`, `20261009T200034218Z`). T251 added 3 Edit Mode guards (`AndroidBackGestureTests`) and 1 player test (`RunInBackgroundPlayerTests`). Against the last full run before this task (T122, `3cf078d`: Edit 1502, Play 1492, Player 23) the T245 to T251 merges added Edit +20, Play +38, Player +1.

**Console check** (Unity logs of the Edit Mode, Play Mode and player runs on `ac4256c`; the first run gave the same result): 0 `error CS`, 0 missing-script lines, 0 `NullReferenceException`. Every error-level message is an intended diagnostic of a fault-injection or failure-path test:

* Edit Mode: `[IdleFactory.Persistence] ... rejected at ...` lines (IOException, `SaveStructureException`, `SaveSemanticException`, schema version, recovery), produced by the Save V1/V2 store, mapper and import fault suites.
* Play Mode: 6 `SaveV2.Deserialize rejected at save.v2.structure` and 1 lexical rejection (corrupt-save startup tests); 2 `panel.prestige.failed` and 1 `panel.build.failed` (ArgumentNullException) from `RunPanelCompositionTests.APanelThatCannotBeBuiltKeepsItsPlaceholderAndReportsACodedDiagnostic` and `SlotGridTests.AnUnregisteredPanelShowsTheNotAvailableStateInsteadOfNothing`; 1,370 warnings "No Theme Style Sheet set to PanelSettings" from test rigs that create bare `PanelSettings` (test harness only; the shipped scene has a theme).

The Unity runs re-serialized `ProjectSettings/ProjectSettings.asset` and the player run left an `InitTestScene*.unity` in the worktree; both are run artifacts and were discarded, not committed.

## 4. Quickstart §1 to §9

| § | What was run | Result |
|---|---|---|
| 1 Content and rules without a scene | Full Edit Mode (section 3, both runs), which contains `RunContentValidatorTests`, the simulator equivalence suites, the IP table and the command rejection suites, and the Feature 001/002 legacy suites | **PASS** (1519 of 1519) |
| 2 Author a tuning change | Run once, on `8a8e518` (T251 changed no content or trace code). Scratch copy of `iron-factory-content.json` with the Assembler cost base 78 → 90, the Gear contract cash base 4000 → 4500 and `contentRevision` 2 → 3. `IDLEFACTORY_CONTENT_CHECK_PATH=<scratch> scripts/test-unity.ps1 -TestFile .../RunContentAuthoringCheckTests.cs`; then the copy was placed over the shipped file temporarily and `BalanceTraceTests` run with `IDLEFACTORY_REGENERATE_BALANCE_TRACE=1` | **PASS.** The authoring check validated the scratch document as content version 2 (1 of 1). The regenerated trace differs from the golden (run 1 eligibility 6,940 s → 7,040 s, FV at eligibility 10,019,139 → 10,009,470; first Gear unchanged at 720 s), and the gate `ReviewRecordQuotesTheMeasuredBandsOfTheCurrentTrace` failed as designed: a tuning change cannot pass without updating the balance record. The shipped content and the golden trace were restored with `git checkout` and verified clean. |
| 3 Play the loop | Full Edit and Play Mode and the tagged player suite (section 3): `RunJourneyPlayerTests` drives Ore → Bar → Plate → Gear → contract → Item → Research → Overdrive → prestige (scaled threshold) → Industry → faster second run → restart on real persistence. Touch on the emulator: section 5. | **PASS (scripted).** The manual keyboard walkthrough of steps 1 to 9 was **not repeated by the parent**; each step has a named automated test (`feature-review.md`, Test/QA matrix: FR-004, FR-006, FR-012, FR-014, FR-017, FR-016/FR-019, FR-020, FR-022) and the owner's partial playtest (`playtest-flo-20261009.md`) played the early first run by hand up to about $40,000 Factory Value, stopping before the first Gear. The first prestige has never been human-played. |
| 4 Durable transitions | `PrestigeTransitionTests`, `SaveV2StoreTests`, `V1ImportTests` (Edit Mode) and `RunTransitionPlayerTests` (player) | **PASS** |
| 5 Offline boundaries | `OfflineSegmentationTests`, `RunLifecycleTests`, `OfflineSummaryPanelTests` | **PASS** |
| 6 Balance trace | `BalanceTraceTests` in the full Edit Mode run, golden comparison byte for byte; regeneration exercised in §2 | **PASS.** Golden (content revision 2, blob `66f41fb`): run 1 first Gear 720 s (12 min), eligibility 6,940 s (115.7 min); run 2 eligibility 4,640 s (77.3 min); runs 3 to 5: 4,640, 4,320, 4,320 s. Summary in `balance-trace-r2.md`. |
| 7 Performance W003 | `W003PerformanceTests` (Edit Mode) and `W003PlayerTimingTests` (player) on the committed fixtures | See the SC-008 table below |
| 8 Android emulator | `scripts/android-emulator-journey.ps1 -Scenario F003` on a new APK of `ac4256c` | Section 5 |
| 9 V1 import | `V1ImportTests` (each committed V1 fixture: accepted 01 to 05 import once with mapped stock, Smelters and proportional progress; `06-w001-scale` fails with `SlotsExceeded`; over-capacity, over-slot, unsupported-ID and corrupt fixtures fail with their codes and leave V1 byte-identical), `RunBootstrapTests` and `StartupStatusPanelTests` (bootstrap path, failed rule and the OD-3 actions Retry and Start new run), `RunTransitionPlayerTests` (packaged fixture in the player) | **PASS.** The per-fixture run goes through the import operation in Edit Mode; the Play Mode bootstrap path is shown for the packaged fixture and the blocked cases, not for each fixture file. |

### SC-008 measurements (Windows development player, Mono, `ac4256c`)

Fixture hashes match the committed records (W003-B `7e278091…8be0`, W003-S `0467d293…ab73`, W003-R `7cb3d7a7…22d`). The emulator was shut down during this run (quiet host). The first run on `8a8e518` had the emulator running and measured 13 to 18 percent slower (B 1,154.8, S 361.5, R 207.3 ms); that is host noise, because `Assets/IdleFactory/Core` is identical in `3cf078d` (T122), `8a8e518` and `ac4256c`.

| Budget | Workload | Measured on `ac4256c` | T122 (`3cf078d`) | Target | Verdict |
|---|---|---|---|---|---|
| Host frame p95, 600 frames | B / S / R | 0.040 / 0.019 / 0.017 ms | 0.041 / 0.017 / 0.018 ms | ≤ 1 ms | PASS |
| Snapshot rebuilds by `StructureRevision` | B, S, R | 4 for 4 structural changes | same | ≤ structural changes | PASS |
| Advance path `GC.Alloc` | B, S, R | 0 samples in 300 frames | 0 | 0 | PASS |
| Whole frame incl. HUD read path | B, S, R | 6 frames with samples in 300 (once per simulated second) | 6 | 0 steady state | **Accepted (D2)** |
| 8 h reconcile, median of 5 (algorithm) | **W003-B** | **965.7 ms** (2,764,800 completions) | 951.7 / 1,017.6 ms | ≤ 500 ms | **MISS, accepted (D1)** |
| | W003-S | 319.7 ms (691,200 completions) | 319.4 / 331.7 ms | ≤ 500 ms | PASS |
| | W003-R | 174.6 ms | 175.5 / 181.8 ms | ≤ 500 ms | PASS |
| Full cold start incl. file I/O, median of 5 | B / S / R | 998.6 / 356.1 / 211.5 ms | 988.1 / 1,047.2 ms (B) | (context) | — |
| Passes per timestamp | B / S / R | max 2 / 2 / 4 | same | ≤ 13 | PASS |

## 5. Android emulator (emulator-only)

Device: `emulator-5554`, AVD `Medium_Phone_API_36.0` (API 36, x86_64 with ARM64 translation `libndk_translation.so`, host GPU, OpenGL ES 3.1 on an RTX 5070 Ti), cold-booted (`-gpu host -no-snapshot-load -no-boot-anim`), gesture navigation, 1080x2400 at 420 dpi, font scale 1.0. **Emulator-only**: no physical Android, no iOS.

### 5.1 First run on `8a8e518`: Android Back regression (history)

APK of `8a8e518` (ARM64 IL2CPP Development, GLES3, profiler-free; SHA-256 `E9533722…239C3C3D`). The full F003 journey passed every step except `BackGesture`: with the Remove confirmation open, a single `KEYCODE_BACK` and the two-swipe edge gesture closed the task (logcat `Transition CLOSE`, `onDestroy`, `Quitting process`), while Back with no surface open kept the app. The T247 baseline APK (`B4C871FD…BEDD6B`) passed the identical check on the same emulator in the same session. Other figures of that run: 8 h return median 768 ms, producing 61.272 fps (controls 61.1), Remove and Prestige confirmation rotations PASS. Decision D8: fix first. T251 isolated the cause to the Player setting `runInBackground` (turned on by T250) and fixed it (setting off, desktop players apply it at runtime); evidence in `android-emulator-evidence.md`, T251 section; independent QA PASS on `5f37644` and `a6e3a33`.

### 5.2 Run of record on `ac4256c`

APK `test-results/t133r2/f003-dev.apk` (local): ARM64 IL2CPP Development, GLES3, `profiler autoconnect False`, 51,052,231 bytes, SHA-256 `5ADB2E0A696F8DE3D2D98B23D041AE44F22074CA14637FB51CB635F06B18B0DE`.

Commands (`-Command` invocation, so the tap arrays arrive as arrays):

```
& ./scripts/android-emulator-journey.ps1 -Scenario F003 -Apk test-results/t133r2/f003-dev.apk -LeaseOwner t133-final-acceptance -BuildTaps @('306,786','528,1853') -RemoveTaps @('1012,1196','400,260','540,2172') -BackInput Keycode -OutDir test-results/t133r2/journey-keycode
& ./scripts/android-emulator-journey.ps1 -Scenario F003 -Apk test-results/t133r2/f003-dev.apk -LeaseOwner t133-final-acceptance -Steps Preflight,Install,Launch,TouchBuild,BackGesture -BuildTaps @('306,786','528,1853') -RemoveTaps @('1012,1196','400,260','540,2172') -BackInput Gesture -BackSwipes 2 -OutDir test-results/t133r2/journey-gesture2
```

Both journeys: **PASS**.

| Check | Result | Raw value |
|---|---|---|
| Install, `pm clear`, launch, ARM64, GLES 3.1 | PASS | |
| Touch build of one Miner | PASS | 0 → 1 machine, cash 37 → 7, same process |
| Rotation during the Remove confirmation | PASS | `ROTATION_90`, same process, no diagnostics, nothing spent (cash 7, 1 machine) |
| Back (`KEYCODE_BACK`) on the Remove confirmation | **PASS** | danger-button pixels 29,637 → 0, same process, machine kept; screenshot: dialog closed, focus ring on Remove, Machine sheet intact |
| Back (edge gesture, 2 swipes, immersive mode) | **PASS** | 29,637 → 0, same process, nothing spent |
| Back with no surface open | recorded | process alive in both runs (closes the Machine sheet, as in T247) |
| Rotation during the Prestige confirmation (`prestige-ready` set) | PASS | same process, nothing spent (run 1, 10 machines, prestige 0 before and after); screenshot: "Cancelled by rotation", focus on "Sell the factory", no Welcome back |
| 8 h return on `w003-s-shipped`, median of 3 | **PASS, 735 ms** [730, 749, 735] | budget ≤ 2,000 ms; production continues afterwards |
| Producing-factory average FPS (profiler-free) | **PASS, 61.397 fps** | budget ≥ 60; same-session controls 61.354 / 61.376 |
| Log scan | PASS | 0 crash signatures in 22,672 lines, 0 coded diagnostics |

The W003-B event bound was not run on the emulator (open in PR-1, D1 option A).


## 6. FR and SC outcomes

The FR-by-FR test matrix is in `feature-review.md` (Test/QA verdict, written on `6fbdafa`); every named test is in the suites that passed in section 3 on `ac4256c`. Outcomes on the accepted commit:

| Req | Outcome | Label and note |
|---|---|---|
| FR-001 to FR-012 | Met | Direct tests; FR-001 negative scope indirect (no belts or other families exist) |
| FR-013 | Met | Deterministic unlock tests and trace; human timing partial (playtest), bands are orientation per spec session 2026-10-09 |
| FR-014 to FR-022 | Met | Direct tests |
| FR-023 | Met | First Industry upgrade affordable at the threshold IP; run 2 faster in the trace (77.3 min vs 115.7 min) and in the player journey (scaled threshold; margin thin, T121-F3) |
| FR-024 | Met | 40 Edit Mode fault cases and 4 player transition tests |
| FR-025, FR-026 | Met | Offline suites; emulator 8 h return (section 5) |
| FR-027, FR-028 | Met | Save V2 and V1 import suites; OD-7 downgrade limitation documented (section 7) |
| FR-029, FR-030 | Met | Content suites; §2 authoring check |
| FR-031 | Met | Indirect (negative scope) |
| FR-032 | Met with exemption | D3 (world slots below 44 pt); D6 (blocker below the fold at 200 % text) is a known limitation |
| FR-033 | Met, emulator-only | Rotation cancels Remove and Prestige confirmations without spending; after the T251 fix, Android Back cancels the confirmation (key; edge gesture on the second swipe in immersive mode, owner decision of 2026-10-09 option a); physical device not claimed |
| FR-034 | Met for provisional art | Art Bible v0.1, briefs, approvals; AD-1, AD-4, AD-6 deferred (D7) |
| FR-035 | Met | Checked math to 10^18, formatter |
| SC-001 | Met | `BalanceTraceTests.Sc001…` and `RunJourneyPlayerTests` (player, scaled threshold) |
| SC-002 | Met | Equivalence suites |
| SC-003 | **Target, scripted trace** | Trace: first Gear 12 min, eligibility 115.7 min (bands 10–15 and about 120 min). Not an observed human time: the playtest stopped at about $40,000 FV, before the first Gear and before any prestige; the owner treats the bands as orientation (spec session 2026-10-09) |
| SC-004 | **Target, scripted trace** | Trace: run 2 eligibility 77.3 min (band 70–80). No human second run |
| SC-005 | Met | V1 import suites, §9 |
| SC-006 | Met | Edit 1522, Play 1530, Player 24 on `ac4256c`, 0 skips, no relevant console errors; Android emulator-only; iOS deferred |
| SC-007 | Met with exemption | Editor layout, focus and text-scale suites at all screen classes; 44 pt = 7 mm on the emulator; D3 |
| SC-008 | **Met for the shipped workloads; W003-B miss accepted (D1)** | Windows S 320 ms and R 175 ms within 500 ms; emulator 8 h return 735 ms (≤ 2 s) and 61.4 fps (≥ 60); W003-B 966 ms against 500 ms, follow-up Issue |

## 7. Release note (OD-7, from `security-devops-review.md` scenario 3)

> **Save compatibility between Feature 002 and Feature 003 builds (development saves only).**
> Feature 003 keeps its progress in its own save folder, `IdleFactory/Checkpoints/v2/`. On the first start it imports the Feature 002 save from `IdleFactory/Checkpoints/v1/` once. It never changes the `v1/` files. The two builds do not share progress after that import:
> 1. **Feature 002 build after the import.** A Feature 002 build reads only `v1/`. It shows the factory as it was at the import and does not see any Feature 003 progress. That progress is not deleted; it stays in `v2/`.
> 2. **Feature 003 build after a Feature 002 build saved to `v1/`.** Feature 003 never imports again once a valid `v2/` save exists. Progress that a Feature 002 build saved to `v1/` after the import is ignored and is lost to Feature 003.
>
> Use one build per device and test profile. To start Feature 003 again from newer Feature 002 progress, copy the `IdleFactory/Checkpoints/v2/` folder somewhere safe, then delete it; the next Feature 003 start imports the current `v1/` save again. This discards the Feature 003 progress. Neither build detects or warns about a mismatch.

## 8. Open risks

| ID | Risk | Source | Handling |
|---|---|---|---|
| PR-1 | W003-B 8 h reconcile 0.97 s against 500 ms on Windows (1.15 s with the emulator running on the host); never run on the emulator; IL2CPP release unmeasured | `performance-review.md` | Accepted (D1); follow-up Issue: measure first, then optimize |
| PR-2 | Once-per-second HUD allocation | `performance-review.md` 4.2 | Accepted (D2) |
| F-1 | Safety checkpoint on focus loss is not rate-limited | `security-devops-review.md` | Follow-up Issue (with F-2 and F-A1) |
| F-2 / F-A1 | No test drives a failing safety checkpoint through `RunLifecycleAdapter`; quit-then-focus-lost ordering | `security-devops-review.md`, Architect verdict | Follow-up Issue |
| F-3 | No timer autosave while unfocused | `security-devops-review.md` | Confirmed by the owner (D5) |
| F-4 | Generation lock is in-process only (two player processes on one `persistentDataPath`) | `security-devops-review.md` | Documented: one instance per profile |
| F-5 | Android Auto Backup not configured or verified | `security-devops-review.md` | Check at the first external Android release build, with the final company and product names |
| F-6 | A confirmed discard overwrites the damaged V2 files without a copy | `security-devops-review.md` | Accepted owner decision, no action |
| F-A4 | Three reason codes used in code are missing from the contract | Architect verdict | Follow-up Issue |
| F-A2 / F-A7 | `RunPersistence` / `RunSnapshots` split and `RunState` mutator visibility | Architect verdict | Follow-up Issue, before F005 |
| ECO | Ore-only probes for runs 2 to 5 and a contracts-only probe; F005 telemetry and playtest list | Game Systems verdict | Follow-up Issue; list carried into the F005 spec |
| PLAY | The first prestige and a second run were never human-played; loop monotony and pacing notes 5 to 9 of the playtest | `playtest-flo-20261009.md` | F004/F005 backlog (`docs/product/ideas.md`) |
| UI/ART | N-5 (D4), item f (D6), F-9 (D3), AD-1, AD-4, AD-6 (D7), N3, C1, TA-2, other polish | `ui-ux-review.md`, `art-direction-review.md` | F004 |
| N-10 | No end-to-end test of a short return with an expired effect | `ui-ux-review.md` | Optional small test Issue |
| T251 | Desktop players now apply `runInBackground` at runtime; the player test proves the initializer runs, but a live Alt-Tab on a desktop player and editor Play Mode while unfocused were not observed, and the mechanism by which the Player setting broke Android Back is not explained | Issue #350 QA warnings | Accepted; the stale T250 wording (wiki `desktop-focus-lifecycle`, `security-devops-review.md` T250-6, T250 task text) is corrected in the T134 ingest |
| EMU | Every Android figure is emulator-only; no physical Android, no iOS | spec, constitution | Physical-device and iOS evidence deferred (Issue #39) |

## 9. Follow-up Issues

To be opened after this record (T133 conditions): performance measurement and hot-path optimization (PR-1, D1 option A); safety-checkpoint failing-write test, quit-then-focus-lost order and throttle (F-1, F-2, F-A1); the three missing reason codes (F-A4); `RunPersistence`/`RunSnapshots` split before F005 (F-A2, F-A7); Ore-only probes for runs 2 to 5 (ECO); carrying the telemetry list into F005.
