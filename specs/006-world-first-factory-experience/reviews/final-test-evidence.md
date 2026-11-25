# F006 final test evidence (T542, Issue #714)

**Role**: Test/QA, verification only (no production code, test, scene or asset change). **Commit under test**: code identical to `origin/main` 9ca6dbb7; main later moved to 6983b005 by PR #775, which is docs only (`git diff --stat 9ca6dbb7 6983b005 -- Assets Packages ProjectSettings` is empty). Unity 6000.6.0f1, Windows host, AVD `IdleFactory_Test_API_36` (API 36, arm64-v8a via translation, host GPU), APK from the same code. All emulator numbers are exploratory workstation evidence (FR-027), not a device-qualification claim. Evidence files: `reviews/final-test-evidence/` (logs, journey folders, `device/` captures at half resolution), `reviews/final-test-names.txt`.

## 1. Result table

| # | Check | Result | Evidence |
|---|---|---|---|
| 1 | Edit Mode, full | **PASS** 3167/3167, 0 skipped | `final-test-evidence/editmode.log` (exit 0), `unity-log-summary-editmode-playmode.txt` |
| 2 | Play Mode, full (not 0 tests) | **PASS** 3483/3483, 0 skipped | `final-test-evidence/playmode.log` (exit 0) |
| 3 | Windows player `-StandaloneWindowsPlayer` | **PASS** 82/82 | `final-test-evidence/standalone.log` (exit 0), `PlayerPlayMode-results.xml` |
| 4 | `scripts/test-art-pipeline.ps1` | **PASS** (exit 0, `ART_PIPELINE_PASS`) | `final-test-evidence/art-pipeline.log` |
| 5 | `scripts/check-settings-noise.ps1 -Commit` | **PASS** on 76b7ee16 (the evidence commit) and on 8eec44bf; the fix-round commit is re-checked, see section 11 (details of the first commit: Unity-run noise in `ProjectSettings.asset`, the URP global settings and `Assets/UI Toolkit` was restored or deleted after every run and never committed) | command output |
| 6 | Console (Unity logs) | **PASS** no `error CS`; tests ran with Unity's log check; APK build log: `Build Finished, Result: Success.`, 0 `error CS` | `unity-log-summary-editmode-playmode.txt` (the full Unity logs are 6 to 8 MB and not committed), `apk-build.log` |
| 7 | Test manifest | recorded | section 3, `final-test-names.txt` |
| 8 | Save fixtures / balance trace | **PASS** | section 4 |
| 9 | Guidance re-run (SC-001, 002, 003, 010) | recorded, before to after in `baseline.md` | section 5 |
| 10 | Emulator Default | **PASS** | `journey-default/journey.md` |
| 11 | Emulator F003 | **FAIL** only on `ProducingFps` (40.6 fps against 60); every other step PASS or INFO | `journey-f003-run1/journey.md` |
| 12 | Emulator F004 | **FAIL** on `F004Crispness` landscape (94.2 % uniform, floor 95 %) and `ProducingFps` (42.3 fps) | `journey-f004-run1/journey.md` |
| 13 | Emulator F005 | steps **PASS** in `journey-f005-run3-nofps` (all steps but the FPS capture); the full run `journey-f005-run2` FAILs `ProducingFps` (39.4 fps) and one Sell flake (see 6.3) | `journey-f005-run2`, `-run3-nofps` |
| 14 | Emulator F006 (`-Scenario F006 -AutosaveCheck`) | **FAIL** `F006Build` only (set has no empty location, see 6.2); Start, Select, Upgrade, HubContracts, Autosave (C-65, C-70), Zoom, LogScan PASS; BuildRotation PASS only with the MACHINE card open, not the build card (the set had no empty location; C-84, re-run in T561 re-verification) | `journey-f006-run2/journey.md` |
| 15 | Manual real-input checklist on the Windows player | **NOT COVERED** (owner decision 2026-11-19, no host input injection). Emulator/adb input coverage in 6 and 7 stays | - |
| 16 | StandalonePlayer two-write autosave case (C-65 part, C-74) | **NOT COVERED** (the case does not exist, no player-compatible RunPersistence rig) | - |
| 17 | Device sweep D1 to D9, markers | section 7 | `final-test-evidence/device/` |
| 18 | Covers SC-007, SC-009, SC-011 | see section 8 | |

The lease `unity,emulator` was held by the parent (owner `issue-714`) and renewed once by me; I did not release it. The emulator and the adb server are stopped, the display size, density, font scale and auto-rotate are reset, and the game was uninstalled only by the journey scripts' own `pm clear` (no other app touched).

## 2. Commands

| Command | Exit | Note |
|---|---|---|
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity.ps1 -Suite EditMode -TimeoutSeconds 3000` | 0 | `UNITY_TEST_PASS suite=EditMode tests=3167 passed=3167 skipped=0` |
| same with `-Suite PlayMode` | 0 | `tests=3483 passed=3483 skipped=0` |
| same with `-StandaloneWindowsPlayer` | 0 | `suite=PlayerPlayMode tests=82 passed=82 skipped=0`; no player process of mine was left running (checked with `Get-CimInstance Win32_Process`) |
| `powershell -NoProfile -File scripts/test-art-pipeline.ps1` | 0 | `ART_PIPELINE_PASS` (the venv was re-created; nothing committed) |
| `Unity.exe -batchmode -quit -executeMethod IdleFactory.Editor.PlatformBuildCommands.BuildAndroidArm64DevelopmentGles3 -noProfilerAutoconnect -buildOutput <apk>` after deleting `Library/Bee/Android` | 0 | APK 46,722,286 B, SHA-256 `62dbdb293f522fed2b7c63b87683028e188a991a11bb638577ef3ae2960d7c5f`, `profiler autoconnect False`, IL2CPP ARM64 OpenGLES3; deleted after use |
| `scripts/android-emulator-journey.ps1 -Avd IdleFactory_Test_API_36 -LeaseOwner issue-714 -Scenario <Default|F003|F004|F005|F006> ...` | see table | logs in `final-test-evidence/journey-*.log` |

Before the first run I deleted the foreign `Library/Bee` DAG files (workstation pitfall). `Library/Bee/Android` did not exist.

## 3. Test manifest (method of `specs/004-visual-identity/reviews/final-test-evidence.md` section 3)

`final-test-names.txt` holds two sections of sorted `fullname | result` lines (LF in the file). SHA-256 over the section lines joined with CRLF and a trailing CRLF:

| Section | Tests | SHA-256 | Results |
|---|---|---|---|
| Edit Mode | 3167 | `bdfc76400c06186fdf417934d7bc3f4fafdb6c248db4939f19d5d1a7e0cba66b` | all Passed |
| Play Mode | 3483 | `f709abb0a2c2e389f4cb8933ab21e38691fc8d7368be25c5bdbc9366d7f57b69` | all Passed |

Player suite (82): ArtFallback 5, DisplayPreferencesPlayer 1, EconomyJourneyPlayer 1, F006Allocation 7, FrameRateTargetPlayer 1, HarnessSmoke 1, HubPlayer 1, JsonContentPlayer 4, MachineCardPlayer 1, PixelCrispness 6, PresentationAllocation 7, ReducedMotion 4, ReducedMotionWorld 3, RunInBackgroundPlayer 1, RunJourneyPlayer 1, RunTransitionPlayer 4, SaveV3PlayerRoundTrip 3, SessionLogAllocation 4, SessionLogFailurePlayer 2, TilemapSmoke 2, W003PlayerTiming 12, W005PlayerTiming 5, W006VRealScenePlayer 1, W006VWorldPlayer 5. No two-write autosave case (C-74). `SessionLogAllocationTests` did not flake.

## 4. Fixtures and balance trace

`git diff --stat c568d24 HEAD` (c568d24 is the pre-F006 main of the T502 baseline) over `Assets/IdleFactory/Tests/EditMode/Fixtures/SaveV1`, `SaveV2`, `SaveV3` and `Balance` is empty: no save fixture and no balance trace changed in F006. The Edit Mode suite (BalanceTraceTests, DeviceSetShippedCatalogTests, the SaveV2/V3 round-trip and re-save tests, PublicApiSnapshotTests) passed, so the fixtures load under F006 and re-save with the same bytes, and the golden trace is unchanged. On the device the committed `w003-s-shipped` and `prestige-ready` sets were pushed with matching SHA-256 and accepted by the player (reconcile accepted in 8 s).

## 5. Guidance re-run

`BaselineMatrixTests` ran inside the full Play Mode suite in probe mode (it wrote to the temporary cache, the `baseline/` JSON of c568d24 is untouched). Before and after tables are in `baseline.md` section "T542 (#714)". Summary: persistent labels 13 to 10 on every class (SC-010 target 6 not met), no-panel world share unchanged on desktop and tablet and +0.5 to +12 points on phones, SC-002 unobscured share improved on every landscape phone class, flat or slightly lower on portrait phones, SC-003 interactions identical on every class and scale (Market, Contracts, Items, Research, Prestige 2 to 3 because the Office hub adds one step; upgrade 3, build 3, one stopped machine 1).

## 6. Emulator journeys

AVD only, adb input, no host input. Build: APK above. Host state during the runs (PF-9 deviation): Chrome (about 50 processes), Docker Desktop, Steam, Battle.net, Riot client, several Codex and node processes were running, host CPU about 36 %; I did not stop other programs.

### 6.1 Default and F003

Default: PASS (Launch, Rotation, Home, Lock, ColdRestart, LogScan; no crash signature, no persistence diagnostic, 2 benign `E Unity` lines). F003: TouchBuild, RemoveConfirmRotation and BackGesture are INFO (no `-BuildTaps`, `-RemoveTaps`; the script skips them as designed), PrestigeConfirmRotation PASS (hub entry taps worked; nothing spent), OfflineReturnTiming PASS (median 292 ms), `ProducingFps` FAIL 40.571 fps.

### 6.2 F006 and the calibration (C-70)

The first F006 run (`journey-f006-run1-uncalibrated`) failed Select, Upgrade, HubContracts and Build because every tap fell on the Welcome back dialog: the shared Welcome back Continue tap (`0.85,0.738`) was below the real button, which sits at y 0.766 once the dialog lists the 9 location rows of the W003-S offline summary. Calibrated fractions (adb screencap, 1080x2400 portrait, screenshots in the run folders):

| Tap | Before | After | Evidence |
|---|---|---|---|
| Welcome back Continue (F004 and F006 start, line 1990) | 0.85,0.738 | **0.855,0.766** | `journey-f006-run1-uncalibrated` (dialog still open) and `journey-f006-run2/02-f006-start.png`-`04` |
| `-F006UpgradeTaps` default | 0.5,0.84 | **0.5,0.815** | the Upgrade button spans y 0.79 to 0.84; 0.84 hit the gap below it (`journey-f006-run2/04-f006-select-card.png`, level sum raised in run 2) |
| F005 Accept of the Timed bar contract (line 1748) | 0.133,0.885 | **0.133,0.70** | the Contracts card docks lower than the old full-height sheet; `journey-f005-run2/05`, `06`, `07` |
| Select (0.5,0.45), hub entry (0.15,0.972), Contracts row (0.5,0.565), build card type row (0.5,0.78), Confirm (0.72,0.90), zoom-in (0.063,0.86), F005 Auto-deliver toggle (0.426,0.653) | - | unchanged | verified on the screenshots (each tap hit its control) |

Test-tool change beyond tap fractions (binding carry C-70, one line, line 2340): the F006Autosave restore check now also requires `restored.Generation -gt periodic.Generation`. Before, `Wait-CheckpointV2` returned the unchanged periodic file on timeout and the check passed on it. I did not touch anything else in the script.

F006 run 2 results: Start PASS; Select, Upgrade (level sum 331 to 332), HubContracts (hub, Contracts, Back) PASS; BuildRotation PASS (portrait to landscape to portrait, same process, no coded diagnostics) but with the MACHINE card open, not the build card: the set has no empty location, so the build-card tap opened a Miner card (see F006Build below); **Autosave PASS (C-65, C-70)**: before force-stop gen 11 level sum 332, periodic checkpoint without a lifecycle event after 35 s gen 13 level sum 333, same process, `am force-stop` without HOME, relaunch restored gen 14 with level sum 333 (a newer generation than the periodic one, so the restore check was satisfied by a real write); Zoom INFO (buttons stay hidden on touch without the preference; `-ZoomPrefTaps` were not supplied because the Settings screenshot `17-f006-zoom-0-settings.png` shows the preference below the fold and no tap was calibrated); LogScan PASS (0 crash, 0 coded diagnostics, 0 unexpected `E Unity`).

**F006Build FAIL, structural.** The committed device sets (`w003-s-shipped`, `prestige-ready`) have a machine on all 12 locations of the placed factory, so `-F006EmptySlotTaps` taps a Miner and the machine card opens instead of the build card (`journey-f006-run2/11-f006-build-1-card.png`). The scenario cannot pass its build step on that set. The build card itself works on a fresh run (6.4). Open for the owner: add a device set with an empty location, or drop the step from the default F006 list.

### 6.3 F004 and F005

- F004: Restyled PASS, portrait Crispness PASS (n=3, 98.3 % uniform), **landscape Crispness FAIL 94.2 %** (floor 95 %; n=2 device px, edge concentration 0.93/0.948). The band 0.25,0.6 now holds the red shortage markers and the F006 deck props; the portrait result at n=3 is fine. Candidate cause: marker or prop sprites at a non-integer scale in the band (checklist item 12, F3). Not proven; needs a marker-free band or a pixel check of the marker scale (T543/T544). `ProducingFps` FAIL 42.325.
- F005: run 1 (`-uncalibrated`) failed the contract steps because of the Accept fraction (6.2). Run 2: Sell tapped "Auto-sell: On" (the only primary button, because the Sell button is disabled while auto-sell empties the stock), so `sale-manual-any` is missing from the session log export: a flake of the primary-button heuristic of `Invoke-F005Sell`, not a product defect. Run 3 (`-run3-nofps`: all steps but the FPS capture) PASS. `ProducingFps` FAIL 39.442 in run 2.

### 6.4 Frame rate (all scenarios)

Producing and the fresh-run controls read about the same in every scenario: F003 40.6, F004 42.3, F005 39.4 (controls 38.9 and 39.4, same process). The B4a session (performance-b4.md) read 61.41 producing and 61.43/61.60 for its controls. Because the controls with no machine fell with the producing capture, the drop comes from the session (host load listed above, emulator state), not from F006 content. I did not stop the owner's programs and I did not take an ABA against an older build (disk: 11 GB free), so a regression is neither proven nor ruled out. The budget row stays unmet in this session; T544 should repeat the measurement on a quiet host with a cold-booted emulator.

## 7. Device sweep (C-73 D1 to D9) and the marker checklist

Screen classes: reachable on the AVD through `wm size`/`wm density`/`user_rotation` and `font_scale` (the game kept its process through every change): 360x640 (720x1280 at 320 dpi), 640x360 and 844x390 (landscape of 720x1280 and 780x1688 at 320 dpi), tablet 768x1024 and 1024x768 (1536x2048 at 320 dpi), the emulator's 1080x2400 and 2400x1080 at 420 dpi, font scale 1.0 and 2.0. Classes **not reachable**: any touch device with a notch or a real safe-area inset (the AVD reports no cutout), other density buckets than the two used, 150 % text. Captures in `final-test-evidence/device/`: `s1-*` (360x640 at 200 %), `c-<class>-start|card`, `f-*` (fresh run, build card), `d9-*`, `m*` (markers).

| Item | Result | Evidence |
|---|---|---|
| D1 640x360 at 200 %, build card Confirm/Cancel vs the dev-build label, 48 pt targets | **PARTIAL, not reached**: the build card was exercised only on the fresh run in portrait (1080x2400 at 100 %): Cancel and Confirm are about 122 px tall (about 46 dp at 2.625 px/dp), type rows about 125 px (about 47 dp); the landscape 640x360 build card was not captured (only the W003-S set was loaded in that class, it has no empty location) | `f-1080x2400-100-buildcard.png` |
| D2 "Overdrive Ready" at 360x640/200 % and "Research" at 844x390/200 % | **PASS for Overdrive Ready**: both words whole inside the button (`s1-360x640-200-start.png`, tight against the Development Build label). **Research hub card not captured** at 844x390/200 % (only the factory view was) | `s1-360x640-200-start.png`, `c-phone-844x390-200-start.png` |
| D3 card with MissingInput+StoreFull and SourceTooSlow | **NOT REACHABLE via adb**: the committed sets give only "Short on Iron Ore, Auto-sell is selling it" (MissingInput with StoreFull cause line, readable, wraps to 4 lines at 640x360/200 %, no overdraw). The SourceTooSlow combination needs a state no fixture builds | `c-phone-640x360-200-card.png`, `s1-360x640-200-card.png` |
| D4 plate tint distinguishable (phone, tablet, desktop) | **PASS by eye, not measured**: occupied, empty and locked plates are distinguishable on phone and tablet captures (`f-...-fresh.png` shows locked plates dimmed with level numbers); no grayscale or deuteranopia simulation was run, desktop class not reachable | `f-1080x2400-100-fresh.png`, `c-tablet-*` |
| D5 store-full marker at n=2 and n=3 | **PASS by eye** in the F005 W005-L captures (orange storage-glyph markers above the miners, outside the frames): `journey-f005-run2/05-f005-contract-offers.png`; no pan/zoom shimmer test (adb has no two-pointer input) | |
| D6 shortage marker shows the resource icon | **FAIL**: every shortage marker (W003-S, all classes) shows the generic down-arrow glyph, not the Iron Ore icon (`m0-start.png`, `c-*-start.png`). Matches the open C-73 note (no code path found) | `m0-start.png` |
| D7 world position around the location tap, one Back after Expand factory | **PARTIAL**: with the empty location tapped, the lattice stays where it was behind the build card (rows at the same y in `f-...-fresh.png` and `f-...-buildcard.png`, no jump), and the ghost plus the selected frame are visible above the card. After Confirm the build card hands over to the machine card and the lattice moves up about 180 px (`f-...-built-t3.png`), the same recentre as opening a machine card from no card. Judge against the 120 px carry in the UI/UX review. Tablet side card and the Expand-factory Back not exercised | `f-1080x2400-100-*.png` |
| D8 every control at least 48 pt at 100/200 % on three compact classes | **NOT REACHABLE via uiautomator**: the Unity player exposes no accessibility nodes (`c-emu-1080x2400-200-start.xml` is an empty frame hierarchy), so no bounds can be read. From screenshots at 1080x2400/420 dpi the Upgrade, Details, Cancel and Confirm buttons are about 118 to 122 px (about 45 to 47 dp); this is just under 48 on the 100 % portrait class and needs the UI/UX reviewer's decision on the unit (dp or pt) | `f-1080x2400-100-buildcard.png` |
| D9 welcome-back gaps | **PASS**: gaps of 9 s and 59 s show no modal; 61 s shows "Welcome back" with Away for 1 min 1 s (the 9 s and 59 s screenshots are identical to the factory view) | `d9-gap-9s.png`, `d9-gap-59s.png`, `d9-gap-61s.png` |
| Marker 1, 2 | PASS (no marker in the fresh run; shortage markers present within 1 s of load, 9 shortage markers in the W003-S set) | `f-...-fresh.png`, `m0-start.png` |
| Marker 7 taps (adb) | (a) tap and a 120 ms press on the icon centre **FAIL** (no card, no selection); (c) tap 40 px beside the icon inside the 48 pt circle **FAIL** (nothing); (d) far empty world: not conclusive (no card was open); strip indicator chip tap **PASS** (card opens). The shortage marker is not tappable in this build | `m1-tap-marker-icon.png`, `m1b-press-marker-icon.png`, `m2-tap-near-marker.png`, `m4-tap-strip-chip.png` |
| Marker 11 accessibility route | **NOT REACHABLE via adb** (no accessible nodes, see D8); TalkBack not run | |
| Marker 8, 9, 10, 12, 13 | 13: PASS (logcat free of marker or other errors, LogScan). 12: not graded (needs a zoom crop). 8 reduced motion, 9 rotation re-position timing, 10 recovery: **not run** | |
| T539 build card (portrait 100 %) | PASS: ghost and frame above the card, type rows visible, selected row Miner, Confirm builds, cash 37 to 7, hand-over to the machine card with focus on the disabled Upgrade (not Overdrive); rotation with the build card open NOT RUN (the journey's rotation step had the machine card open) | `f-...` |
| T540 banner / stage | not run (no level-up fixture) | |

## 8. Success criteria, UI rule and empty/error states

- SC-007, SC-009, SC-011 (test base and journeys): the full suites pass, the fixtures and the trace are unchanged, the F006 journey is calibrated. The remaining device/FPS findings are listed in section 9.
- Accessibility: automated accessibility-tree checks are impossible (no accessible nodes through adb); keyboard and focus behaviours are covered by the Play Mode suites (NavigationInput, focus tests, all passing); the 48 pt claim could not be measured on the device (D8).
- Supported screen classes: sections 7 and 5; all ten classes of the probe matrix, plus seven of them on the device at 100 and 200 % text.
- Empty/error states seen: fresh run (hint bar "Build a Miner: tap an empty location.", locked plates with unlock levels, disabled Upgrade with "Need $29"), Contracts locked card, Market "Nothing to sell" with the disabled Sell button, offline summary on a fresh run (61 s) and on the 8 h cap. Persistence error path: the journeys report no `persistence.failed` and no coded diagnostics.

## 9. Open findings (for T543, T544, T548)

1. D6 shortage marker uses the generic arrow, not the resource icon (C-73 still open).
2. Marker tap does not select the first affected location (marker checklist 7a, 7c), the strip chip does.
3. F004Crispness landscape 94.2 % (floor 95 %), possibly the marker scale (F3).
4. Producing FPS about 40 in every scenario with controls equal: host load; repeat on a quiet host (T544); B4a recorded 61.4.
5. F006Build cannot pass on a set without an empty location; add a set or change the default steps.
6. Journey tool: F005Sell picks Auto-sell when Sell 1 is disabled (flake); the F006 hub/Continue tap fractions are calibrated for the 1080x2400 portrait AVD only (landscape and zoom-preference fractions remain provisional: `-ZoomPrefTaps` not supplied).
7. Hand-over from the build card to the machine card recentres the world by about 180 px (D7); targets measure about 45 to 47 dp on the 100 % portrait class (D8, screenshot measurement, +-2 dp).
8. C-76 notes: (a) `ShellNavigationStack` doc comment still lists "dialog, pinned value, settings, sheet (Details), machine card" while the stack now also holds the hub and Industry frames (wiki `f006-batch-p3a-card-hub-and-gate-fixes.md`); (b) the machine card's cause and rates refresh only on a state, shortfall or completion change, a rate-only change lags (not exercised on the device); (c) a `CompletionsDelta` written in a rebuild frame is lost (no production reader); (d) there is no bootstrap-level cold-start test beyond T560's C-60 case; the F006 journey's ColdRestart and Autosave relaunch show a cold start loading with no diagnostics on the device. (e) `familyFpsCaps`: `WorldCompositionSettings.NewDefaultFamilyFpsCaps()` returns `{machine.assembler, 8}`; the removed class default (commit 87f4ac01) was `{ "machine.assembler", NormalFps }` with `NormalFps = 8`; `MachinePresentationModelTests` asserts the 8 for both the settings default and the shipped table. Confirmed equal.
9. C-65 part: periodic checkpoint on IL2CPP/Android confirmed (6.2); the StandalonePlayer two-write case is not covered (C-74).

## 10. Not covered

The manual real-input checklist on the Windows player (owner decision 2026-11-19). TalkBack. Physical devices, notch or cutout classes, 150 % text. The Research hub card at 844x390/200 %, the 640x360/200 % build card, the tablet build card, level-up banner and stage triptych (no fixture), reduced-motion and rotation timing of markers. Pinch zoom (adb has one pointer).


## 11. Fix round (QA PASS-with-findings on 8eec44bf): records only

**Logs.** `*.log` is gitignored (`.gitignore:116`). Force-added (each under 1 MB, nothing trimmed): `final-test-evidence/editmode.log`, `playmode.log`, `standalone.log`, `art-pipeline.log`, `journey-default.log`, `journey-f003-run1.log`, `journey-f004-run1.log`, `journey-f005-run1-uncalibrated.log`, `journey-f005-run2.log`, `journey-f005-run3-nofps.log`, `journey-f006-run1-uncalibrated.log`, `journey-f006-run2.log`; also `apk-build.log` (the Unity APK build log, 739 KB) and `PlayerPlayMode-results.xml` (872 KB). The Edit Mode and Play Mode Unity logs (6.3 and 7.6 MB) and result XML (2.5 and 3.3 MB) exceed 1 MB and are **not** committed; `unity-log-summary-editmode-playmode.txt` carries their error counts and result headers, and `final-test-names.txt` the per-test results. The `editmode.log`, `playmode.log` and `standalone.log` files are the runner's short stdout (about 0.5 KB each). The player's own Unity log is not committed.

**Row 5.** `scripts/check-settings-noise.ps1 -Commit 8eec44bf` (the final commit before this round): `Settings noise check: OK`. It is re-run on the fix-round commit after this commit; the result is in the PR report.

**s1-* captures.** Committed in 76b7ee16: `device/s1-360x640-200-start.png` and `device/s1-360x640-200-card.png` (`git ls-files` lists both).

### Verdicts added (pass, fail, not run or not reachable)

Marker checklist (T543-C), items 3 to 6 and the rest:

| Item | Verdict | Reason |
|---|---|---|
| 3 keep-out (marker box covers no machine, plate cell, badge, selected card band; first and last row; two shortages in a row) | **NOT RUN** | No overlap analysis and no first/last-row selection against markers. By eye the markers sit in the lane gaps beside machines in every capture (`m0-start.png`, `c-*-start.png`); that is an observation, not a pass |
| 4 backdrop tone under the marker (A_ov, grayscale, icon identifiable at n_min) | **NOT RUN** | No grayscale or contrast measurement; the icon is also the generic arrow (D6 FAIL) |
| 5 draw order (marker above cues and pips; Overdrive machine) | **NOT RUN** | No Overdrive machine in any loaded state; no pip overlap was looked for |
| 6 store-full marker vs the depot FULL sprite | **NOT RUN (partial by eye)** | Store-full markers (orange storage glyph) appear in the F005 W005-L captures and differ from the red shortage markers; the depot FULL sprite state (copper lip) was not reached or compared |
| 1, 2 | PASS | section 7 |
| 7 | (a) FAIL, (c) FAIL, (d) not conclusive, strip chip PASS | section 7; (b) machine at the junction side was not tapped: **NOT RUN** |
| 8, 9, 10, 12 | NOT RUN | section 7 |
| 11 | NOT REACHABLE via adb | no accessibility nodes |
| 13 | PASS | LogScan |
| U1 (band flex-wrap at 640x360/200 % with a sheet open) | **NOT RUN** | no sheet was opened in that class; only the machine card (`c-phone-640x360-200-card.png`, the side column wraps its text without clipping) |
| A10 (selected footprint visible with the band) | **PASS by eye** | the blue selected frame is visible above the card in `s1-360x640-200-card.png` and beside the side column in `c-phone-640x360-200-card.png` |
| C-12/C-31 look items (gantry 9 px, floating stage-3 props, V float, pip over body, 5 px depot clip) | **NOT RUN** | no pixel-level inspection; no floating prop was noticed in the captures (not a verdict) |
| T529 pop placement (C-24) | NOT RUN | no pop trigger |

T539 build card, items 2 to 9:

| Item | Verdict | Reason |
|---|---|---|
| 2 landscape 2400x1080 100/200 % and 640x360 200 % (Cash/store/Overdrive tappable, ghost in the world column, card not clipped, buttons >= 48, reason lines) | **NOT RUN** | the build card was opened only on the fresh run in portrait; the landscape classes ran on the W003-S set, which has no empty location |
| 3 tablet side-panel dock, first column not clipped | **NOT RUN** | same reason |
| 4 60 % ghost vs dark plate; available/selected/occupied/locked distinguishable | **PASS by eye** (portrait 1080x2400, 100 %) | `f-1080x2400-100-buildcard.png`: the selected location shows the ghost with a blue frame, available plates are outlined in blue, occupied/locked plates are dimmed with level numbers; not measured |
| 5 shortfall "Need $X more" below the buttons; disabled Confirm not primary | **NOT RUN** | the Miner was affordable (cash 37, cost 30); the "Need $29" seen later is the machine card's Upgrade line, not the build card |
| 6 build with Cash: dust, hand-over to the machine card, focus to Upgrade, no world jump; reduced-motion static highlight | **PARTIAL** | hand-over PASS and focus ring on the (disabled) Upgrade button PASS (`f-...-built-t3.png`); dust not seen in the 1 s and 3 s captures; the world recentres about 180 px on hand-over (D7); reduced motion NOT RUN |
| 7 Back/Escape, hub open while card open, rotation with the card open, retarget keeps the type | **PARTIAL** | Back after the build closed the machine card (card band changed in `f-...-after-back.png`); Back on the build card itself, hub while open, rotation with the build card open (the journey rotated the machine card) and retarget: **NOT RUN** |
| 8 TalkBack/keyboard order, tooltips, Overdrive focus guard (A3) | **NOT REACHABLE via adb** | no accessible nodes; the Overdrive guard was not exercised |
| 9 warning/danger text contrast on the card surface | **PARTIAL by eye** | the yellow "Unlocks at factory level 3." and "Short on Iron Ore" lines are legible on the dark card in `f-...-buildcard.png` and `s1-360x640-200-card.png`; no contrast ratio was measured |

T540 milestone banner and stages (UI/UX review 641c5a0):

| Item | Verdict | Reason |
|---|---|---|
| 1 (C1: 640x360, 844x390, 360x640 at 200 %: is a top-row machine or ghost covered by the banner) | **NOT RUN** (mandatory check not done) | no level-up can be triggered through adb with the committed sets; no fixture raises the factory level on the device |
| 2, 3, 4 (banner below strip and notice bar, wrap and dismiss, gone by 2.5 s, reduced motion) | **NOT RUN** | same reason |
| 5 TalkBack announcement | **NOT REACHABLE via adb** | |
| 6 offline return with a level-up while away; prestige gives B-03 | **NOT RUN** | the offline summaries seen (8 h cap, 61 s) held no level-up; prestige was not confirmed on the device |
| 7 stage triptych | **PARTIAL by eye** | Early stage on the fresh run (railing only, no lit ring, `f-1080x2400-100-fresh.png`) and the full-edge stage on W003-S (wall and depot, `m0-start.png`, no floating prop noticed); Partial and Established transitions, "Deck extended." and the lit-ring growth NOT RUN; 12-location keep-out at 640x360 not checked |

These NOT RUN and PARTIAL verdicts stand for T543/T548: the device could not cover them with the committed sets and adb input, and I did not fabricate a fixture.


## 12. T561 re-verification (Issue #782): delta against sections 1 to 11

**Role**: UI/UX Developer with Gameplay Developer (implementer; independent QA and UI/UX review follow). Base `origin/main` 51073a9f. Unity 6000.6.0f1, AVD `IdleFactory_Test_API_36` (headless, host GPU), APK of the final code (44.6 MB, IL2CPP ARM64 GLES3, built after deleting `Library/Bee/Android`, deleted after use). Evidence: `final-test-evidence/t561/` (journey folders with `journey.md`, `journey.json`, `run.log`; `device/` captures at half resolution). All emulator numbers are exploratory workstation evidence (FR-027).

### 12.1 Result table (delta)

| # | Check | Result | Evidence |
|---|---|---|---|
| 1 | Edit Mode, full | **PASS** 3175/3175 (3167 plus 8 new: empty-location set cases, camera model cases) | `scripts/test-unity.ps1 -Suite EditMode`, exit 0 |
| 2 | Play Mode, full (not 0 tests) | **PASS** 3516/3516 (3483 plus 33 new cases) | `-Suite PlayMode`, exit 0 |
| 3 | Windows player `-StandaloneWindowsPlayer` | **PASS** 82/82 | exit 0; no player process of this run left |
| 4 | Emulator Default | **PASS** (run before the fix round; the fix round touches the camera, the Back polling and the journey script only) | `t561/journey-default/journey.md` |
| 5 | Emulator F003 | **FAIL** only `ProducingFps` 40.1 fps (informational here, C-83 goes to T544); same run as row 4 | `t561/journey-f003/` |
| 6 | Emulator F004 (final code) | **FAIL** only `ProducingFps` 58.0 fps (informational); `F004Crispness` portrait **PASS** 99.2 % (n=3), landscape **PASS** 96.3 % (n=4, x 0.10 to 0.90; was FAIL 94.2 %) | `t561/journey-f004/` |
| 7 | Emulator F005 | **FAIL** only `ProducingFps` 40.3 fps (informational); `F005Sell` located the Sell button and `sale-manual-any` is in the session log export; same run as row 4 | `t561/journey-f005/` |
| 8 | Emulator F006 (`-Scenario F006 -AutosaveCheck`, final code) | **PASS**, every step: Start, Select, Upgrade, **HubContracts with a real Back state check (C-85)**, **Build** (build card docked, machine built 11 to 12), **BuildRotation with the build card open**, Autosave, Zoom, LogScan | `t561/journey-f006/` |
| 9 | Settings noise | see the PR report (`scripts/check-settings-noise.ps1 -Commit <sha>`); Unity-run noise in `ProjectSettings.asset` and `Assets/UI Toolkit` was restored or deleted after every run and never committed | |

### 12.2 Per item

| Item | Finding | Fix or record | Device re-check |
|---|---|---|---|
| D6 / C-77 shortage marker shows the resource icon | **Misreading in T542.** The red down-arrows on every machine are the per-machine Missing-input badge (`F003-BADGE-MISSING-INPUT`), not the attention marker. The attention marker is the 22 px resource icon at the trunk junction (the T542 capture `m0-start.png` shows the Iron Ore icon on the right trunk at about (462,482) of the half-size image). The code already drew the resource icon. | No product change. New Play Mode assertions in `AttentionMarkerSceneTests`: the marker sprite is the catalog sprite of the shortage resource and is not the Missing-input badge sprite. | `t561/device/marker-0-factory.png`: the Iron Ore icon at the junction. |
| Marker tap, checklist 7a, 7c / C-78 | **Misreading in T542.** The T542 taps landed on the badge, which lies outside the machine tap shape and is not a tap target. The marker icon itself selects the first affected location. | No product change; the only edit in `AttentionMarkerView.cs` is the XML doc comment of `SetProjectedPixelsPerCell` (the value is in panel points). No marker visual changed (owner decision 2026-11-25: marker salience is an art decision with C-75 after P4). The scene test now also projects the icon centre and a point 6 px beside it through the camera and requires the marker (icon or touch circle) and the first affected location. | Tap on the icon centre (924,963) of 1080x2400: the Smelter card of the first affected location opens ("Short on Iron Ore", `marker-1-tap-icon-card.png`); a tap 40 px beside it also selects it (`marker-2-tap-beside-icon.png`; 40 px is 15 pt at 2.63 px per point, inside the 24 pt radius). These two taps were made by hand with `adb shell input tap` on the first APK of this task; the marker code is unchanged in the final APK. The scene test covers 0 and 6 px (the editor Game view has 1 px per point), not 40 px at device scale. |
| Crispness landscape / C-79 | **Cause found; it is not the 0.917x store-full marker (C-75).** The measurement band is 0.25 to 0.6 of the height over the whole width. In landscape that band also holds the HUD columns (Cash, Store, the item pill, Mine, Overdrive, Office, Settings), whose text and borders are not on the art-pixel grid. They dragged the edge concentration down and the detector chose n=2 instead of 4. Cropping the committed T542 landscape screenshot to the world column (x 0.10 to 0.90) gave n=4 and 96.9 % before any change. The set has no store-full state, so the C-75 marker is not on screen. C-75 is therefore NOT triggered and no art decision is needed for this item. | `scripts/android-emulator-journey.ps1`: `-CrispBandLandscapeX` (default `0.10,0.90`) restricts the landscape measurement to the world column; portrait keeps the full width. No threshold or assertion was relaxed (95 % floor and 0.9 concentration unchanged). | `journey-f004` (final code): landscape 96.3 %, n=4 (band x 0.1 to 0.9, which is 80 % of the width); portrait 99.2 %. The landscape crop is a measurement-window change with every threshold unchanged; **the owner accepted it on 2026-11-25** (conveyed by the parent). |
| Recentre / C-60, C-80, D7 | **Real, fixed.** `CameraScaleModel.PanSlotIntoBand` re-applied `Constrain` even when the slot already lay inside the band. The pan limits follow the band rectangle, so on the hand-over from the build card to the shorter machine card the larger band pulled the same view in by 105 px (1080x2400 at 420 dpi, 100 % text; reproduced by the new device-scale test before the fix). | Architect fix round on the first fix: skipping the clamp on every no-pan path left a stepped-down scale unclamped and unsnapped. Now a scale change always clamps and snaps (OD-9b C1 holds on every path, `PanSlotIntoBand(..., scaleChanged: true)`). With the scale unchanged and the slot already in the band the view stays (visibility first) and the sheet relaxation grows by exactly the overrun of the current position (`KeepCurrentViewLegal`, it ends with the sheet), so the limits contain the view and the next pan or layout change does not jump it. The alternative (silently clamping when the slot stays visible) is the 105 px pull itself. New Edit Mode cases in `CameraScaleModelTests`: an unchanged open leaves the position untouched; a band-grow hand-over for three locations moves at most the band growth, keeps the slot inside the band, `Pan(zero)` is not clamped and a small pan moves by that pan plus one art pixel; a step-down leaves the position on the new pixel grid and inside the new limits. New Play Mode cases `TheHandOverToTheMachineCardMovesTheWorldByNoMoreThanTheCardBandsDifference` (compact classes and phone-portrait 390x844) and `OnTheDeviceTheHandOver...` (1080x2400 and 2400x1080 at 420 dpi, 100 and 200 %, locations 0 and 11): the world moves by at most the shrink of the band and never by more than the machine card's band, the machine stays inside its band and the view is inside its pan limits; 0 px in every case after the fix. **Temporary revert to the pre-T561 `CameraScaleModel.cs` (`git show 51073a9f:...`, restored afterwards, `cmp` identical):** the Play Mode device case `OnTheDeviceTheHandOver...` (device portrait 1080x2400, 100 %, locations 0 and 11) FAILS (2 cases) and the three new band-grow cases in `CameraScaleModelTests` FAIL; everything else passes. | Cross-correlation of the full-resolution captures `d7-0`, `d7-1`, `d7-2`: 0 px from before to build card and 0 px from build card to machine card. The T542 APK showed about 180 px by eye; the same method on the first T561 APK (before the camera fix) gave 132 px. |
| Targets / C-81, D8 | **Not a short control.** The Play Mode tests already assert 48 pt (panel points). At the applied scale of the device (2.63 px per point after the 7 mm touch-target raise at 420 dpi) 48 pt is 126 px, which is 48 dp at the 420 dpi bucket. T542 estimated 45 to 47 dp from half-resolution captures and the cyan fill only, which leaves out the 4 px border on each side of a button. | No product change. New Play Mode case `OnTheDeviceEveryCardControlIsAtLeastFortyEightDpInDevicePixels` (1080x2400 and 2400x1080 at 420 dpi, 100 and 200 %): every build-card control (Confirm, Cancel, type rows) and machine-card control (Upgrade, Details, Close) is at least 125 px tall in device pixels. New `RunDeviceProfileTests` case: 48 pt at the required scale is at least 48 dp of the reported density (7.64 mm against 7.62 mm). | Full-resolution capture `12-f006-build-1-card.png` (border to border): Confirm and Cancel 127 px (rows 2134 to 2260), type rows about 164 px; `13-f006-build-2-confirm.png`: Upgrade 127 px, Details 126 px. 48 dp at 420 dpi is 126 px. The AVD reports xdpi 420.0, density 2.625. |
| Journeys / C-82 | F006Build could not pass; F005 Sell could tap Auto-sell; the Back swipe left the Contracts sheet open; landscape, zoom and hub taps are calibrated on 1080x2400 portrait only. | New committed device set `device/empty-location` (the W003-S shipped state with one Assembler fewer, location 11 empty and unlocked; checkpoint `22222222-2222-2222-2222-000000000032`; SHA-256 data `943f5931ca17827c...`, manifest `77b1a94ece319469...`), generated by `ShippedDeviceSets.BuildEmptyLocation` and pinned by `DeviceSetShippedCatalogTests` (byte identity, cold start over the shipped catalog) and `SaveV3StoreTests`. The F006 scenario uses it by default and `-F006EmptySlotTaps` defaults to `0.715,0.572` (location 11). `Select-SellButtonBands` picks the cyan band that has another button right below it (Auto-sell) and never the toggle itself. `Invoke-BackOnce -Swipes 2` (local to the F006 hub step; `$BackSwipes` is not mutated for other callers) sends two edge swipes, because in immersive mode the first one only reveals the system bars. F006HubContracts now checks the Back STATE: the sheet region differs from the open Contracts view by at least 0.5 (closed reads 0.69 to 0.75, the sheet staying open about 0.2); the hard-coded Close-button tap is gone. `Select-SellButtonBands` returns no band when the Auto-sell neighbour is not visible, so `Sell button located` fails loudly instead of tapping a possible toggle. `-F006EmptySlotTaps` is documented as calibrated to the 1080x2400 test AVD and the empty-location set. Committed set check: `EmptyLocation_HasAnEmptyUnlockedLocation11AfterTheColdStart`. | Landscape, `-ZoomPrefTaps` and hub fractions stay provisional (later, C-82). |
| C-84 | Result-table row 14 listed BuildRotation as PASS without the machine-card qualifier. | Row 14 corrected in section 1 above. | |
| C-85 Android Back on the Contracts sheet | **Real, fixed (owner decision 2026-11-25).** It was not specific to Contracts: the FIRST Back (or any first key) after a launch never reached the shell, whatever was open. On the emulator the hub and Market closed on a later Back; the locked Contracts sheet did not because it was the first Back of the session in every journey. Device diagnostics (temporary log, removed): the Back reached the shell on the second press; a first `adb shell input keyevent 29` (any key) made the next Back work; the first Back was lost 4 of 4 times before the fix. The AVD exposes 16 Keyboard devices (`devices=50 keyboards=16`) and the shell polled only `Keyboard.current`, which did not see the first press. | `RunShellDocument.EscapePressedThisFrame()` reads Escape on every Keyboard device (no allocation) instead of `Keyboard.current` only. New Play Mode `HubContractsBackSceneTests` (shipped scene, real panels): Back on the locked Contracts sheet opened from the hub returns to the factory (the existing shell rules, as `HubPanelTests`), and Escape pressed on a Keyboard that is not the first one also closes it. **The second case is not discriminating in the editor:** it also passes on the old polling (the editor switches `Keyboard.current` with the event), so it pins the behaviour; the cause itself is device evidence. | Device, final APK: `journey-f006` `F006HubContracts` "Back closed the Contracts sheet" PASS, change 0.749 on the FIRST Back of the session (`c85-0-contracts-open.png`, `c85-1-after-first-back.png`); three fresh launches with the fix APK each closed the sheet on the first Back (3 of 3). Why the Input System records the first press on a non-current device is not investigated further. |

### 12.3 Not covered, and notes

- D1 (640x360 at 200 %) and the tablet build card are covered by the Play Mode assertions (compact classes, text scale 100 and 200 %) but were not re-captured on the device; the AVD offers the 1080x2400 portrait and 2400x1080 landscape classes in this task.
- ProducingFps of F003, F004 and F005 on a loaded host (about 40 fps, controls equal) stays informational and goes to T544 (C-83).
- Only one `F005Sell` run was made on the device (Sell 1 selected, `sale-manual-any` present); the selector was also checked offline against the committed T542 Inventory capture (Sell 1 selected, band 0.7225).
- The emulator and the adb server are stopped; wm size, density and font scale were never changed in this task.
- Default, F003 and F005 were run on the code before the fix round (camera clamp, Back polling and journey-script changes); the fix round re-ran F006 and F004 on the final APK plus the three full suites and the Windows player.
- Recentre on the final code: cross-correlation of `d7-0`, `d7-1`, `d7-2` of `journey-f006` gives 0 px and 0 px.


## 13. T563 upgrade marker (Issue #786, FR-027)

**Role**: UI/UX Developer with Gameplay Developer (implementer; independent QA and UI/UX review follow). Base `origin/main` cad6d419. Evidence: `final-test-evidence/t563/` (journey reports, one device screenshot). Emulator numbers are exploratory workstation evidence.

- **What**: the world now draws the upgrade-available marker. `SlotVisual.UpgradeMarker` (affordable next upgrade and no problem badge, the rule of `MachinePresentation.UpgradeMarkerVisible`) feeds one marker renderer per location (`SlotRenderers`, role `UpgradeMarker`).
- **Sprite reused**: `F003-UI-ICON-UPGRADE` (approved F004 UI-icon family, 24 x 24, 16 x 15 opaque pixels, near white). Drawn at scale 1 (no resampling, C-75), no new art, no atlas or importer change. It is smaller than the 23 x 27 badge and never red (Edit Mode pixel test).
- **Placement and priority**: centre at art pixel (26, row 21), left of marker box M1; it never covers the badge or the Overdrive marker box. It yields to an Item boost (boosted machine keeps its marker; at most two markers per location). It follows the marker zoom rule (hidden below 24 px per cell), shares the boosted marker's sorting order (below the badge), is not a tap target.
- **Updates**: only on model change. `RunShellPresenter` compares cached upgrade costs with the cash each frame (loop over the locations, no allocation) and rebuilds the visuals only when an upgrade becomes affordable or stops being so. No scene or prefab change (the shipped catalog already holds the icon as a required row).

| # | Check | Result |
|---|---|---|
| 1 | Edit Mode, full | PASS 3187/3187 (3175 plus 12 new) |
| 2 | Play Mode, full (`-TimeoutSeconds 3000`) | PASS 3522/3522 (3516 plus 6 new, incl. 2 real-scene cases) |
| 3 | `-StandaloneWindowsPlayer` | PASS 83/83 |
| 4 | Emulator F006 `-AutosaveCheck` | PASS every step (`t563/journey-f006`) |
| 5 | Emulator F004 | Crispness PASS portrait 98.9 % (n=3), landscape 97.6 % (n=4); FAIL only `ProducingFps` 33.0 fps (informational, C-83 to T544) and `Environment adb server stable` (adb restarted during one pull, Unity-independent) (`t563/journey-f004`) |
| 6 | Device screenshot | `t563/device/marker-affordable-and-problem.png`: white upgrade marker above the three running Miners of the top row (affordable), none on the machines under a red Missing-input badge |
