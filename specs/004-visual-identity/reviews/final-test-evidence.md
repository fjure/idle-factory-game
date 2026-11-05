# F004 final Test/QA evidence (T442, Issue #560)

Evidence runner: Test/QA (Medium tier). Nothing in this record repairs product code. Tested commit: **`701e8c85c1fed3461181075d2f78f08f1d353f9a`** (origin/main, merge of #664/T475; worktree `.maqa-worktrees/issue-560-t442-test-qa-evidence`, branch `maqa/issue-560-t442-test-qa-evidence`). This record and its attachments are the only paths the evidence commit adds; the code under test is unchanged. Unity 6000.6.0f1, Windows host, emulator AVD `Medium_Phone_API_36.0` (API 36, 1080x2400, 420 dpi, host GPU, GLES 3.1). Leases `unity,emulator` were held by `issue-560` (parent) for the whole run; `gpu` unused.

## 1. Result summary

| # | Check | Result |
|---|---|---|
| 1 | `scripts/test-unity.ps1 -TimeoutSeconds 3000` Edit Mode | exit 0, **2566 passed**, 0 failed, 0 skipped |
| 2 | same, Play Mode | **2572 passed**, 0 failed, 0 skipped (not 0 tests) |
| 3 | `scripts/test-unity.ps1 -StandaloneWindowsPlayer -TimeoutSeconds 3000` | exit 0, **62 passed** (tagged `StandalonePlayer`), 0 failed |
| 4 | `scripts/test-art-pipeline.ps1` | exit 0, **220 passed** (first call exit 3, stale venv launcher, repaired as documented) |
| 5 | Console check of the three Unity logs | no unexpected error, exception or assert (section 6) |
| 6 | Camera scale n per screen class, DEV-1/DEV-2 | recorded (section 7). **DEV-2 confirmed and wider than written** (finding F1) |
| 7 | IL2CPP Android GLES3 development APK + emulator journey (F004) | APK built; installed build verified; journey result in section 8: the scripted `F004Crispness` step FAILED (fixed-coordinate dismissal missed the Welcome-back sheet) and passed only on a rerun after the sheets were dismissed by hand; the other scripted steps (Launch, W004VPush, F004Restyled, Home, Lock, ColdRestart, F004LogScan) passed; not a clean scripted journey PASS |
| 8 | Real two-finger pinch on the emulator | **not verified** (cannot be injected: `sendevent` Permission denied, no root) |

Gaps and findings that need a decision are in section 10 (F1 to F5).

## 2. Commands, exit codes, durations

| Command (cwd = worktree) | Exit | Duration | Evidence |
|---|---|---|---|
| `powershell -NoProfile -File scripts/test-unity.ps1 -TimeoutSeconds 3000` | 0 | 09:05:19 to 09:20:07 local (14 min 48 s wall, Edit Mode started 08:06:22Z, Play Mode 08:09:08Z) | `UNITY_TEST_PASS suite=EditMode tests=2566 passed=2566 skipped=0`, `suite=PlayMode tests=2572 passed=2572 skipped=0`; `ART_PIPELINE_SKIPPED` chain step (no art paths changed against the merge base) |
| `... scripts/test-unity.ps1 -StandaloneWindowsPlayer -TimeoutSeconds 3000` | 0 | about 2 min (09:20:32 to 09:22) | `UNITY_TEST_PASS suite=PlayerPlayMode tests=62 passed=62 skipped=0` |
| `... scripts/test-art-pipeline.ps1` (1st) | 3 | | `Failed to canonicalize script path`, `ART_PIPELINE_TOOLING_UNAVAILABLE` (stale `pytest.exe` launcher in the ignored `tools/art-pipeline/.venv`; exit 3 is a blocker, not a pass) |
| `uv sync --frozen --offline --reinstall-package pytest --project tools/art-pipeline` | 0 | | documented fix (`docs/art/F004-technical-art-integration.md` section 6, wiki batch 7/10 pitfalls); ignored directory, nothing committed |
| `... scripts/test-art-pipeline.ps1` (2nd) | 0 | 48.31 s | `220 passed`, `ART_PIPELINE_PASS`; `test-results/art-pipeline/report.json`: status `pass`, exitCode 0, `commit` = `701e8c85...`, `dirty` false, 2026-11-05T08:22:55Z |
| `Unity.exe -batchmode -quit -projectPath . -executeMethod IdleFactory.Editor.PlatformBuildCommands.BuildAndroidArm64DevelopmentGles3 -noProfilerAutoconnect -buildOutput <apk>` | 0 | about 20 min incl. Android re-import | APK `f004-t442.apk`, 46,389,823 B, SHA-256 `c9e265434e82e7e183dae2bce4acce864126115dab759ac1520feb1767094c42` (outside the repo; budget cap 47,345,615 B, section 4 row 10 of `performance-budgets.md`: 0.96 MB below the cap) |
| `scripts/android-emulator-journey.ps1 -Scenario F004 ... -LeaseOwner issue-560` | see section 8 | | |

The full run needed no rerun and did not return 0 tests in Play Mode. The leases were not released by me (parent owns them).

## 3. Test manifest (method of `baselines-b1-manifest.md` section 1)

Attached: [final-test-names.txt](./final-test-names.txt), two sections of sorted `fullname | result` lines (ordinal sort; **the committed file has LF line endings**, see below). All 2566 and 2572 lines end in `| Passed`. Per-section SHA-256 of the list lines without the section header, one line each, **joined with CRLF and terminated with a trailing CRLF** (the hashes are over CRLF text, not over the committed LF bytes; the method reproduces the B1 Edit Mode hash `0ecef7c6...08d79` from the B1 file, so the line format is the same). To reproduce from the committed file: take the lines of one section without its `##` header and run `tr -d '\r' | sed 's/$/\r/' | sha256sum` (Edit Mode `sed -n '2,2567p'`, Play Mode `sed -n '2570,$p'`; both verified to give the hashes below):

| Section | Tests | SHA-256 |
|---|---|---|
| Edit Mode | 2566 | `67ab58a38a492eb05cdbbe484b8ed69bfba0d65a8bf80b33399a9c79db00f5fd` |
| Play Mode | 2572 | `d1abed558dd5d80745ee7c899d50bae7dea820208023b977de69d3396f9de23c` |

Comparison with `baselines-b1-test-names.txt` (B1: Edit Mode 2323, Play Mode 2230), by test name without result: Edit Mode **+246 names, 3 names removed** (net +243); Play Mode **+372 names, 30 names removed** (net +342), by class:

- Edit Mode added: DisplayPreferencesStoreTests 72, F004AssetContractTests 49, CameraScaleModelTests 37, PresentationAnimationClockTests 18, RenderPipelineConfigurationTests 16, StateCueSetTests 15, SlotGridLatticeTests 14, PresentationCatalogFallbackTests 10, ScreenClassScaleTableTests 8, PresentationSpriteCatalogTests 4, UiIconIntegrationEditTests 3. Removed: PresentationSpriteCatalogTests 3 (renamed or replaced cases).
- Play Mode added: RunHudLayoutTests 60, UiIconIntegrationTests 48, StateCueOverlayTests 48, SlotGridTests 29, TextScaleLayoutTests 26, MachinePanelGuidanceTests 26, RenderParityTests 21, ScreenClassScalePlayTests 19, SettingsSheetTests 17, NavigationInputTests 17, ContractsPanelTests 16, PanelTitleWrapTests 12, PresentationAllocationTests 8, PixelCrispnessTests 6, OfflineSummaryPanelTests 5, ArtFallbackTests 5, ReducedMotionTests 4, ReducedMotionBootstrapTests 4, UiIconBlindNamingCaptures 1. Removed: MachinePanelGuidanceTests 16, SlotGridTests 13, NavigationInputTests 1 (parameter or name changes of re-baselined cases).
- Arithmetic check: Edit Mode 2323 + 246 - 3 = 2566; Play Mode 2230 + 372 - 30 = 2572. Names counted per test case (parameterised cases count once per parameter set, so a re-parameterised case shows as removed plus added). The name-level diff of the removed cases (SC-006) is in [final-test-rebaseline.md](./final-test-rebaseline.md).

No test ran under another result than Passed in either list.

## 4. Windows player suite (`-StandaloneWindowsPlayer`)

62 of 62 passed. Class tally (names): ArtFallbackTests 5, EconomyJourneyPlayerTests 1, FrameRateTargetPlayerTests 1, HarnessSmokeTests 1, JsonContentPlayerTests 4, PixelCrispnessTests 6, PresentationAllocationTests 8, ReducedMotionTests 4, RunInBackgroundPlayerTests 1, RunJourneyPlayerTests 1, RunTransitionPlayerTests 4, SaveV3PlayerRoundTripTests 3, SessionLogAllocationTests 4, SessionLogFailurePlayerTests 2, W003PlayerTimingTests 12, W005PlayerTimingTests 5. The F004 cases of plan.md "Test strategy" are in it (crispness, reduced motion, art fallback, 600-frame W004-V allocation run).

**`RestyledSceneSmokePlayerTests` does not exist in the repository** (finding F2): no file, no class, no git history; it appears only in `plan.md` line 118 and in the canonical T442 text. The player result XML has no case of that name. The tagged cases above ran and passed; the planned "restyled journey smoke" is covered only by the existing journey tests (`RunJourneyPlayerTests`, `EconomyJourneyPlayerTests`) and by the emulator journey in section 8. Step 3 of the assignment (targeted run of that file) is therefore impossible, not skipped.

After the run, no `PlayerWithTests.exe` or `UnityCrashHandler64.exe` was running (checked with `Get-CimInstance Win32_Process`), nothing to stop.

## 5. Settings noise and pipeline assets

After the full run, after the player run and after the Android build, `git status` showed `ProjectSettings/ProjectSettings.asset` modified (empty iOS icon slots re-serialized, the known signature) and, after the Android build only, `Assets/IdleFactory/Rendering/UniversalRenderPipelineGlobalSettings.asset` with one `m_SettingsList` entry removed (`rid: 3338746865761648669`). Both were restored with `git checkout` and are not committed. `ShaderGraphSettings.asset` never changed. Caveat (finding F5): the APK was built while that URP settings asset still carried the removed entry, so the build used the re-serialized state; the APK is 46,389,823 B, in line with the earlier 46.3 MB baseline, but the shader variant count was not measured here.

## 6. Console check

Scanned `EditMode.log`, `PlayMode.log`, `PlayerPlayMode.log`. No `error CS`, no compile error, no unhandled exception: tests run with Unity's log check, so any unexpected error, exception or assert log would have failed its test, and 0 tests failed. Lines that match an exception search are expected, test-injected failures: `[IdleFactory.Persistence] ... rejected at ... (IOException)` (persistence failure tests), `injected unexpected failure` NullReferenceException, `panel.prestige.failed ... (ArgumentNullException)`, `Run safety checkpoint failed and was contained`, and `presentation.scale.belowFloor: n=1 below n_min=...` at 360x640, 640x360, 640x480 and 844x390 (the intended degrade log of `RunHudLayoutTests.F8_...`, `RunPanelCompositionTests` and `SlotGridTests.TheZoomLimitsComeFromTheSettings...`). The player log has one `[Licensing Module] Error: Access token is unavailable` (host licensing, not the game). All three logs end with `Killing ADB server` (active build target was Android from the earlier build, as the pitfalls predict); the emulator went `offline` and was restarted from the main checkout path (`adb kill-server; adb start-server`) before the emulator work.

Note: stack traces in the logs name the path of the worktree `issue-659-t475-settings-polish`. The warm `Library/` was compiled there; the commit under test is the merge of exactly that branch (#664), so sources are identical. No claim beyond that.

## 7. Camera scale n per screen class, DEV-1 and DEV-2

Sources: Play Mode `ScreenClassScalePlayTests` (real shell, real HUD, `TryGetUnobstructedScreenRect`; it prints `n_min`, `n_fit`, largest fitting scale per physical device, with and without ore-vein overlay and hint bar; both rows are listed, smaller rect = overlays on), and Edit Mode `ScreenClassScaleTableTests` (fixed HUD model, D = 24 rows for the point-size classes at the scales 1.68, 2.0, 2.625 and 3.0 px/pt). The slack column is derived arithmetic (rect from the printed integer-rounded size minus `n_fit * (extent + 24)` art px; lattice 70 x 54, extent 210 x 216 art px on 3 columns, 280 x 162 on 4 columns; 24 art px is the half-cell margin on both sides), so it is exact only to about 1 px.

### 7a. Real shell (Play Mode, asserted: `largest >= n_min`, controller scale equals `n_fit`) (the `n_min` value itself is asserted only for the 1280x720 landscape phone; for the 720x1280 portrait phone it is derived and logged)

| Device | px/pt | HUD rect (overlays on / off) | n_min | n_fit | Slack at n_fit (limiting axis) |
|---|---|---|---|---|---|
| phone-portrait 720x1280 xhdpi (320 dpi) | 2.0045 | 624x871 / 624x1068 | **2** (derived, logged, not asserted) | 2 | 156 px width |
| phone-landscape 1280x720 xhdpi | 2.0045 | 1000x440 / 1184x508 | **2** | 2 | 68 px height (on) / 136 px (off) |
| phone-portrait 1080x2400 420 dpi (emulator) | 2.6309 | 953x1859 / 953x2121 | 3 | 4 | 17 px width |
| phone-landscape 2400x1080 420 dpi | 2.6309 | 2031x711 / 2273x801 | 3 | 3 / 4 | 153 px height (on) / 57 px (off) |
| **tablet-portrait 1536x2048 264 dpi** | 2.0222 | 711x1818 / 711x1949 | 3 | **3** | **9 px width (4.5 pt)** |
| tablet-landscape 2048x1536 264 dpi | 1.8667 | 1287x1324 / 1287x1444 | 2 | 4 | 71 px width |
| desktop 1280x720 | 1.0 | 872x605 / 872x671 | 1 | 2 | 233 px height (on) |
| desktop 1920x1080 | 1.5 | 1308x909 / 1308x1006 | 2 | 4 | 92 px width |

Tablet 1536x2048 carry-forward: n = 3 holds with a width slack of about 9 px (1.3 percent of 711 px, 4.5 pt) at the 70x54 lattice with the real shell. The carry-forward figure of 0.8 px is not reproduced; the printed width is integer-rounded, so treat my figure as 9 px, plus or minus 1 px, and not as a contradiction until the independent QA reads the unrounded rect. Either way n = 4 does not fit (needs 936 px) and n = 3 does.

### 7b. Point-size classes (Edit Mode fixed-HUD table, D = 24, n_min / n_fit per panel scale 1.68 / 2.0 / 2.625 / 3.0)

| Class | 1.68 | 2.0 | 2.625 | 3.0 |
|---|---|---|---|---|
| phone-portrait 360x640 | 2 / 2 | 2 / 2 | 3 / 3 | 3 / 3 |
| phone-landscape 640x360 | 2 / 2 | 2 / 3 | 3 / 3 | 3 / 4 |
| phone-portrait 390x844 | 2 / 2 | 2 / 2 | 3 / 3 | 3 / 4 |
| phone-landscape 844x390 | 2 / 2 | 2 / 3 | 3 / 4 | 3 / 5 |
| tablet-portrait 768x1024 | 2 / 2 | 2 / 3 | 3 / 3 | 3 / 4 |
| tablet-landscape 1024x768 | 2 / 3 | 2 / 4 | 3 / 5 | 3 / 6 |
| desktop 1280x720 | 2 / 4 | 2 / 5 | 3 / 6 | 3 / 6 |
| desktop 1920x1080 | 2 / 6 | 2 / 6 | 3 / 6 | 3 / 6 |

(Desktop windows at 1.0 and 1.5 px/pt are in 7a.) Gap F3: **640x480 is asserted by no n test** (only by the `RenderParityTests` frames, which use the Feature 003 fractional fit-all and log `ppc=46.74`, not an integer n); and 1024x768, 768x1024, 360x640, 390x844 and 844x390 are asserted only by the fixed-HUD model, not by the real shell. `RenderParityTests` logs `RENDER-FRAME` pixels per cell per class: fit-all 360x640 32.00, 390x844 35.08, 640x360 30.71, 844x390 33.94, 640x480 46.74, 768x1024 36.92, 1024x768 48.63, 1280x720 64.00, 1920x1080 64.00 (these are the B2 parity harness, not the shipped integer scale). No test code was added.

### 7d. Art Bible section 4.3 verdict

Cue icon = 23 art px at `n_min`, points = 23 * n_min / px per pt.

| Case | n_min actual | Points | Verdict |
|---|---|---|---|
| 420 dpi emulator 1080x2400 | 3 | 26.2 pt | meets (table row confirmed) |
| tablet portrait 1536x2048 | 3 | 34.1 pt | meets |
| tablet landscape 2048x1536 | 2 | 24.6 pt | meets |
| **DEV-1**, 460 dpi phone 1170x2532 / 2532x1170 (Edit Mode model, D = 24: n_min 3, 2.881 px/pt) | 3 | 23.95 pt, 0.05 pt short | **confirmed as written**, at the fixed-HUD model only (no Play Mode real-shell case for 460 dpi, not device-captured) |
| **DEV-2**, 1280x720 phone at 320 dpi (real shell, Play Mode asserts `n_min == 2`, exact ceiling would be 3, 2.0045 px/pt) | 2 | 22.95 pt, 1.05 pt (4.4 percent) short | **confirmed, DEV-2 exists** (the 1 percent tolerance gives n = 2 where the derivation assumed it) |
| DEV-4, lock digit on that phone (15 art px cap * 2 / 2.0045) | 2 | 14.97 pt | confirmed (arithmetic from the asserted n_min; same cause as DEV-2) |

**Finding F1 (DEV-2 is wider than written).** The same 1 percent tolerance gives `n_min = 2` for the **portrait 720x1280 phone at 320 dpi** as well (derived and logged, **not asserted**: Play Mode log `phone-portrait 720x1280 xhdpi: 2,0045 px/pt ... n_min 2 ... n_fit 2`; Edit Mode table D = 24: n_min 2. Only the 1280x720 landscape case asserts `n_min == 2`). Art Bible section 4.3 lists "320 dpi phones, portrait: n_min 3, 34.4 pt, meets" and names DEV-2 only for the 1280x720 landscape phone. With the actual `n_min = 2` the portrait row is 22.95 pt, the same 4.4 percent shortfall as DEV-2, and the lock digit there is 14.97 pt (DEV-4). The deviation size is already accepted by the owner for the landscape phone; the table row and the DEV-2/DEV-4 wording need the portrait phone added (Art Director or owner call). I did not change any document.

## 8. Emulator (Android, emulator-5554)

Build: profiler-free development IL2CPP ARM64 GLES3 APK from this commit (section 2). Device: 1080x2400, 420 dpi, host GPU, GLES 3.1 (NVIDIA RTX 5070 Ti translator). Notes on the run:

- The first scripted run reported `INSTALL_FAILED_INSUFFICIENT_STORAGE` and then ran on an **old, already installed build** (last update 2026-11-04 18:29). Its results are void and are not used. To free space I uninstalled the previous `com.DefaultCompany.idlefactorygame` and a stale scratch package `com.idlefactory.t422temp` (its path was a temporary T422 test build), left `de.hamm.habittracker` untouched, then installed the new APK (`adb install -r` Success, 721 MB free before, 91 percent to 88 percent used). The saved game data of the old install is gone (the journey pushes the fixture anyway).
- Scripted rerun `-Scenario F004 -Steps Preflight,Launch,W004VPush,F004Restyled,F004Crispness,Home,Lock,ColdRestart,F004LogScan -LeaseOwner issue-560`, run 3: Preflight, Launch (GLES 3.1 device, first display 2.0 s), W004VPush (fixture hash `1920dfaf...ecca6fc` recorded, `w003-s-shipped` SHA-256 verified on the device), F004Restyled (accepted reconcile after 8 s, 12 machines, same process through rotation), Home, Lock, ColdRestart (state restored 12 -> 12), F004LogScan (0 crash signatures, 0 coded diagnostics, 3 `E Unity` lines all known Autoconnect probes) all PASS. **F004Crispness FAILED in that run** because the Welcome-back sheet (the script dismisses it with fixed coordinates; the new layout has the Continue button elsewhere) still covered the world, so there was no pixel art in the band (screenshot `e09-welcome-back-not-dismissed-portrait.png`). The 2 failed checks are a harness mismatch with the shell layout, not evidence about the art (finding F4).
- After dismissing both Welcome-back sheets by hand (two taps on Continue), `-Steps Preflight,F004Crispness` on the live restyled factory, **after the manual dismissal** (a rerun, not the scripted run): **PASS**, portrait 1080x2400 `n = 4` device px, edge concentration 1.000 on both axes, 10,825 of 10,825 edge-bearing blocks uniform (100 percent, floor 95 percent); landscape 2400x1080 `n = 4`, 8,611 of 8,611 uniform. This matches `n_fit = 4` for this device in section 7a (crisp integer scale on the device).
- The W004VFps step was not run (needs the profiler-free W004-V-forced build; no performance claim here, T444 owns it).

Carry-forward checks (screenshots in `final-test-evidence/`):

| Carry-forward | Result |
|---|---|
| Zoom button at its limit shows the hint | **verified** on the device (taps via `adb input tap`): `+` at the maximum shows "Maximum zoom", `-` at the minimum shows "Minimum zoom" (`e03`, `e04`). |
| Real two-finger pinch: preview, snap on release, cancel on a third finger | **not verified**. `adb shell sendevent` on the emulator multi-touch devices returned `Permission denied` (shell user, no root); `adb input` cannot do multi-touch. Needs a human or an instrumented injector. The unit/Play Mode coverage (`CameraScaleModelTests`, `SlotGridTests`) ran green but does not use real pointers (pitfall: UI Toolkit drops device pointer events in batch mode). |
| Phone-portrait tap usability on the 90x45 hit diamond | **partly verified**: `adb input tap` (a point, not a finger) at the plate centre at `n_min` (n = 3, hit diamond 270 x 135 device px) opens the sheet for slot 5 and keeps the selected slot above the sheet (`e05`); a tap 115 px right of that centre opened slot 6 (the neighbour whose centre is 95 px away; hit diamonds overlap at the 70 px pitch and the nearest centre won, `e06`). Real-finger tap usability (contact patch, 44 pt rule) is **not verified**. |
| 844x390 first-run landscape with hint bar, ore vein and open sheet | **not captured at 844x390.** The emulator was set to `wm size 720x1280`, `wm density 320`, rotated to landscape (1280x720, 2.0045 px/pt, a 640x360-class phone): first run (cleared data) shows the hint bar "Build a Miner: tap an empty slot", the locked plates with lock-level digits 2 to 5, the Mine control and zoom column without overlap (`e07`); an empty slot tapped opens the Build sheet as a right-hand panel with slot 1 selected and visible, slot 4 partly covered by the panel (`e08`). The separate "ore-vein overlay" is the Mine control at bottom right in this build; it is hidden by the open panel, no clipping seen. 844x390 itself was not set up. |
| TalkBack (zoom hint announced, button tooltip state) | **not run, covered by the accepted R-21** (TalkBack sees only one SurfaceView, owner accepted). |

Device state restored afterwards: `wm size reset`, `wm density reset` (1080x2400, 420), `accelerometer_rotation 1`, `user_rotation 0`, HOME. The installed APK is the T442 build with the first-run state of the manual 1280x720 session; the ADB server runs from the main checkout path. The editor's active build target is **Android** after the build (Library of this worktree); not switched back (no editor session, batch mode only) and no later Unity run depended on it, but the next Unity CLI run here will again end with `Killing ADB server`.

## 9. Success criteria mapping

| SC | Evidence | Status |
|---|---|---|
| SC-003 (100 percent of the F003 presentation keys restyled, 0 placeholders) | Edit Mode `F004AssetContractTests` (49 added names), `PresentationSpriteCatalogTests`, `StateCueSetTests`, Play Mode `UiIconIntegrationTests`, `StateCueOverlayTests`, `ArtFallbackTests` (5, also in the player), all passed on this commit; screenshots `e01`, `e02` (restyled isometric plates, machines, badges, floor on the device). | contributes pass; the shipped-set count is an independent-QA read of the asset tests |
| SC-004 (100 percent of sprites pass the asset checks, complete provenance) | Pipeline suite 220 passed on the exact commit (`report.json` pass, dirty false); `F004AssetContractTests` passed. | contributes pass |
| SC-006 (F003 gameplay, content, save, UI suites pass unchanged in behaviour; re-baselined zoom tests listed) | Full Edit Mode 2566 and Play Mode 2572 green; the manifest delta in section 3 lists added names by class; removed or renamed names (16 `MachinePanelGuidanceTests`, 13 `SlotGridTests`, 1 `NavigationInputTests`, 3 `PresentationSpriteCatalogTests`) are listed one by one, each with its replacement test, reason and commit, in [final-test-rebaseline.md](./final-test-rebaseline.md) (all 33 mapped; 0 without replacement); B1 pre-switch baselines unchanged (`TheB1PreSwitchBaselinesAreUnchanged` in the Play Mode suite). | pass |

## 10. Open gaps and findings

- **F1 (DEV-2 scope)**: see section 7d. Portrait 320 dpi phone also lands at `n_min = 2`, 22.95 pt; Art Bible section 4.3 and DEV-4 need the portrait phone added.
- **F2**: `RestyledSceneSmokePlayerTests.cs` does not exist; the T442 text and `plan.md` Test strategy expect it. The 62 tagged cases passed; the planned class was never written.
- **F3**: no real-shell n assertion for 640x480, 360x640, 640x360, 390x844, 844x390, 768x1024, 1024x768 (only the fixed-HUD model, and none for 640x480); no Play Mode case for 460 dpi (DEV-1 rests on the model).
- **Owner decisions of 2026-11-05**: F1 is resolved by an Art Bible patch (v1.0.4); F2, F3 and F4 are accepted gaps, recorded in `final-acceptance.md`.
- **F4**: `android-emulator-journey.ps1` F004Restyled/F004Crispness does not dismiss the Welcome-back sheet reliably with the current shell layout (a second sheet appears after the rotation steps); the scripted crispness check fails on a covered band. Crispness itself passes once the sheets are dismissed. A script fix is outside this assignment.
- **F5**: the Android build re-serialized `UniversalRenderPipelineGlobalSettings.asset` during the build; the APK was built from that state; shader variant count not measured.
- Not verified: real two-finger pinch, third-finger cancel, real-finger tap on the 90x45 diamond, 844x390 landscape capture, TalkBack (R-21 accepted), W004VFps, shader variants.
- Not run: targeted `-TestFile` for the missing smoke class (cannot exist).

## 11. Host load during the runs

Before the first run: Steam (steam.exe and 8 steamwebhelper), Riot Client (7 processes), Docker Desktop (about 10 processes), about 45 Chrome processes, and the running emulator (`emulator.exe`, `qemu-system-x86_64.exe`); other agent sessions were not measured. CPU load read near the end of the player run: about 79 percent (instantaneous). This is a correctness run, not a timing run; no performance conclusion is drawn and the durations above are not a baseline.
