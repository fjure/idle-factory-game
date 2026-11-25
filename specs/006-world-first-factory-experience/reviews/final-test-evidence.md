# F006 final test evidence (T542, Issue #714)

**Role**: Test/QA, verification only (no production code, test, scene or asset change). **Commit under test**: code identical to `origin/main` 9ca6dbb7; main later moved to 6983b005 by PR #775, which is docs only (`git diff --stat 9ca6dbb7 6983b005 -- Assets Packages ProjectSettings` is empty). Unity 6000.6.0f1, Windows host, AVD `IdleFactory_Test_API_36` (API 36, arm64-v8a via translation, host GPU), APK from the same code. All emulator numbers are exploratory workstation evidence (FR-027), not a device-qualification claim. Evidence files: `reviews/final-test-evidence/` (logs, journey folders, `device/` captures at half resolution), `reviews/final-test-names.txt`.

## 1. Result table

| # | Check | Result | Evidence |
|---|---|---|---|
| 1 | Edit Mode, full | **PASS** 3167/3167, 0 skipped | `final-test-evidence/editmode.log` (exit 0) |
| 2 | Play Mode, full (not 0 tests) | **PASS** 3483/3483, 0 skipped | `final-test-evidence/playmode.log` (exit 0) |
| 3 | Windows player `-StandaloneWindowsPlayer` | **PASS** 82/82 | `final-test-evidence/standalone.log` (exit 0) |
| 4 | `scripts/test-art-pipeline.ps1` | **PASS** (exit 0, `ART_PIPELINE_PASS`) | `final-test-evidence/art-pipeline.log` |
| 5 | `scripts/check-settings-noise.ps1 -Commit` | **PASS** on 76b7ee16 (the evidence commit; Unity-run noise in `ProjectSettings.asset`, the URP global settings and `Assets/UI Toolkit` was restored or deleted after every run and never committed) | command output |
| 6 | Console (Unity logs) | **PASS** no `error CS`; tests ran with Unity's log check; APK build log: `Build Finished, Result: Success.`, 0 `error CS` | `test-results/unity/*/*.log` (not committed), APK build log |
| 7 | Test manifest | recorded | section 3, `final-test-names.txt` |
| 8 | Save fixtures / balance trace | **PASS** | section 4 |
| 9 | Guidance re-run (SC-001, 002, 003, 010) | recorded, before to after in `baseline.md` | section 5 |
| 10 | Emulator Default | **PASS** | `journey-default/journey.md` |
| 11 | Emulator F003 | **FAIL** only on `ProducingFps` (40.6 fps against 60); every other step PASS or INFO | `journey-f003-run1/journey.md` |
| 12 | Emulator F004 | **FAIL** on `F004Crispness` landscape (94.2 % uniform, floor 95 %) and `ProducingFps` (42.3 fps) | `journey-f004-run1/journey.md` |
| 13 | Emulator F005 | steps **PASS** in `journey-f005-run3-nofps` (all steps but the FPS capture); the full run `journey-f005-run2` FAILs `ProducingFps` (39.4 fps) and one Sell flake (see 6.3) | `journey-f005-run2`, `-run3-nofps` |
| 14 | Emulator F006 (`-Scenario F006 -AutosaveCheck`) | **FAIL** `F006Build` only (set has no empty location, see 6.2); Start, Select, Upgrade, HubContracts, BuildRotation, Autosave (C-65, C-70), Zoom, LogScan PASS | `journey-f006-run2/journey.md` |
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

F006 run 2 results: Start PASS; Select, Upgrade (level sum 331 to 332), HubContracts (hub, Contracts, Back) PASS; BuildRotation PASS (portrait to landscape to portrait with the build card open, same process, no coded diagnostics); **Autosave PASS (C-65, C-70)**: before force-stop gen 11 level sum 332, periodic checkpoint without a lifecycle event after 35 s gen 13 level sum 333, same process, `am force-stop` without HOME, relaunch restored gen 14 with level sum 333 (a newer generation than the periodic one, so the restore check was satisfied by a real write); Zoom INFO (buttons stay hidden on touch without the preference; `-ZoomPrefTaps` were not supplied because the Settings screenshot `17-f006-zoom-0-settings.png` shows the preference below the fold and no tap was calibrated); LogScan PASS (0 crash, 0 coded diagnostics, 0 unexpected `E Unity`).

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
| T539 build card (portrait 100 %) | PASS: ghost and frame above the card, type rows visible, selected row Miner, Confirm builds, cash 37 to 7, hand-over to the machine card with focus on the disabled Upgrade (not Overdrive); rotation with the build card open PASS (journey) | `f-...` |
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
