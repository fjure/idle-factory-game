# Android emulator evidence (T124, Issue #206)

**Emulator only.** Every result below was measured on the Android Studio AVD `Medium_Phone_API_36.0` (API 36, x86_64 host with ARM64 translation, host GPU). No physical Android and no iOS claim (iOS deferred, Issue #39). Covers FR-033, SC-006, SC-007, SC-008.

**Verdict (after the rerun of 2026-10-08, see "Rerun on T245 sets"): budgets PASS, one T139 item FAIL.** The first run found that the old `w003-s` set cannot be loaded by the real Player (`save.v2.invariant`, root cause confirmed by T245: 12 machines at factory level 1, which the shipped content limits to 4 slots). The rerun on the shipped-catalog sets measured the eight-hour return at **746 ms median** (budget <= 2 s, PASS) and the producing-factory FPS at **61.145** average (budget >= 60, PASS; same-session control 61.5). The Prestige confirmation rotation passes (manual taps, because the script cannot scroll the sheet). The **OS back gesture does not cancel the Remove confirmation** (FAIL, emulator-injected swipe; KEYCODE_BACK does). No product code, fixture or scene was changed; misses are recorded, not tuned away.

| Item | First run | Rerun (T245 sets) |
|---|---|---|
| 8 h return, W003-S, median of 3, <= 2 s | UNRESOLVED (set rejected) | **PASS 746 ms** [761, 745, 746] |
| Producing FPS >= 60, control | UNRESOLVED | **PASS 61.145**, controls 61.458 / 61.561 |
| Prestige confirmation rotation | UNRESOLVED | **PASS (manual)** |
| OS back gesture = Cancel | UNRESOLVED | **FAIL** (dialog stays open; KEYCODE_BACK PASS) |
| R20 rotation checks, "nothing spent" | script FAIL (old assertions) | **PASS** (corrected assertions) |
| TouchBuild script check | FAIL | still FAIL (no baseline checkpoint on a fresh clear, build itself works) |

Sections below marked "superseded" keep the first-run values for the record.

## Identities

| Item | Value |
|---|---|
| Unity / build | `6000.6.0f1`, Android ARM64 IL2CPP Development Player, OpenGL ES 3 only |
| Build command | `Unity.exe -quit -batchmode -projectPath <worktree> -executeMethod IdleFactory.Editor.PlatformBuildCommands.BuildAndroidArm64DevelopmentGles3 -buildOutput test-results/t124/f003-dev.apk -noProfilerAutoconnect` (profiler-free: `profiler autoconnect False` in the build log; build 00:03:55, success, 0 errors) |
| APK | `f003-dev.apk`, 36,972,397 bytes, SHA-256 `0597F48B3951C51B04F83D15C4D7107D009257DE216C9F82A620F8894A47A9BA` (one APK for every step: profiler-free and Development, so the `ResumeAndReconcile` line exists) |
| Commit built | origin/main `629b630` |
| Application id | `com.DefaultCompany.idlefactorygame` |
| Device | `emulator-5554`, model `sdk_gphone64_x86_64`, fingerprint `google/sdk_gphone64_x86_64/emu64xa:16/BP22.250325.006/13344233:user/release-keys`, ABIs `x86_64,arm64-v8a`, bridge `libndk_translation.so` |
| Boot | cold boot: `emulator -avd Medium_Phone_API_36.0 -gpu host -no-snapshot-load -no-boot-anim` |
| GLES | Android Emulator OpenGL ES Translator (NVIDIA GeForce RTX 5070 Ti), OpenGL ES 3.1; Unity created an OpenGL ES 3.1 device, primary ABI arm64-v8a |
| Host | Windows 11 Pro 10.0.26200; no Unity editor open during the captures; adb server restarted once after the build (`adb start-server`) |
| Leases | `unity` and `emulator`, owner `issue-206`, held by the parent |

Raw files: `android-emulator-evidence/` (`journey-run0..runD.md` are the script reports of the five invocations, screenshots and the rotation logcat). Full per-run folders stay local under the ignored `test-results/t124/`.

## Touch journey (script `-Scenario F003`)

Commands (taps found from `adb exec-out screencap -p` screenshots of the running player, 1080x2400 portrait):

```
& scripts/android-emulator-journey.ps1 -Scenario F003 -Apk test-results/t124/f003-dev.apk -LeaseOwner issue-206 -Steps Preflight,Install,Launch,TouchBuild,RemoveConfirmRotation -BuildTaps @('306,786','528,1853') -RemoveTaps @('540,2172')   # run A, invalid timing, see below
& scripts/android-emulator-journey.ps1 -Scenario F003 -LeaseOwner issue-206 -Steps Preflight,TouchBuild,RemoveConfirmRotation -BuildTaps @('306,786','528,1853') -RemoveTaps @('540,2172')                         # run B, after pm clear + launch + 20 s
```

Tap coordinates: slot 1 `306,786`, then `Build Miner` on the Build sheet `528,1853`, then `Remove` on the Machine sheet `540,2172`; Prestige tab `990,2328`.

| Check | Result | Raw value |
|---|---|---|
| Preflight (device, AVD, ARM64 via translation, GLES3) | PASS | see Identities |
| Install `adb install -r`, `pm clear`, Launch | PASS | PID 4970 (run A); time to graphics device 2 s, first display 1.61 s |
| Touch build of one Miner | **PASS by screenshots and checkpoint, script check FAIL** | Run B `02-touch-after.png` and the later checkpoint show 1 machine, cash 15 (25 - 10). The script check reads `none -> none` because the Save V2 checkpoint is only written on a lifecycle event (focus loss or HOME), not within the script's 45 s wait. First run (A) tapped before the sheet was ready and built nothing. Script FAIL stays recorded. |
| Same process | PASS | PID 5621 |
| Manual journey (fresh run): tap slot -> Build sheet -> Build Miner -> Machine sheet (Running, 21 % cycle) -> Remove -> dialog -> Back | PASS | `s2.png` to `s5.png` |

## Rotation during the Remove confirmation (run B)

| Check | Result | Raw value |
|---|---|---|
| Remove confirmation opens | PASS | `remove-open-portrait.png`: "Remove Miner?" with Keep machine (focused) and Remove |
| Rotated to landscape while open | PASS | `ROTATION_90 2400x1080`, requested `SCREEN_ORIENTATION_FULL_USER` |
| Dialog cancelled, notice shown, focus returned | **PASS** | `remove-rotated-landscape.png`: notice "Cancelled by rotation", dialog closed, the Remove button carries the focus ring, machine sheet intact (side sheet in landscape) |
| Same process | PASS | PID 5621 |
| Nothing spent | **script FAIL, manual PASS** | Script: before checkpoint was `none` (see above). Screenshots: 1 Miner and cash 15 before and after; checkpoint after rotation: `run 1, cash 15, 1 machine, gen 2, prestige 0`. |
| No reconcile line during the rotation | **FAIL (expectation mismatch, product behaviour to review)** | `RemoveConfirmRotation-logcat.txt` shows `windowFocusChanged false/true` during the rotation and then `ResumeAndReconcile ms=30` from `RunLifecycleAdapter.TryResume`. A rotation therefore runs a resume reconcile of the elapsed seconds (30 ms). No offline summary was shown and nothing was spent. The script asserts "no ResumeAndReconcile line"; whether a resume reconcile on a focus cycle is intended must be decided by the Architect / Test lens, it is not tuned away here. |

Rotation during the **Prestige** confirmation: **UNRESOLVED.** The Prestige panel on a fresh run reads "Not ready to sell yet ... Needed to sell: 10.0M, Total Factory Value 49" (`s6.png`), so the sale control cannot be activated, and the only valid-looking high-value state is the W003-S set that does not load (below). The script step was not given taps because there is no confirmation to open.

## W003-S eight-hour return time and producing-factory FPS: UNRESOLVED (superseded by the rerun below)

Command: `& scripts/android-emulator-journey.ps1 -Scenario F003 -LeaseOwner issue-206 -Steps Preflight,W003Push,OfflineReturnTiming,ProducingFps,F003LogScan` (run D; the earlier run C only pushed and launched).

* `W003Push` PASS: device SHA-256 equals the committed `0467d293d69e...` (data) and `84cfe0b4e727...` (manifest); departure is 25.413 h before the device clock.
* **Blocker:** the Player rejects the set. Logcat: `[IdleFactory.Persistence] SaveV2.Deserialize rejected at save.v2.invariant`, then `return.blocked: The saved factory could not be loaded. Retry or start a new run.` and the startup dialog "The saved factory could not be loaded" (`s7.png`: Retry, Use previous save, Discard damaged save). The fixture was authored against the test-local `W003Workload` catalog (README: fixtures 05 to 07 load against it; 12 machines including Press and Assembler in slots 7 to 11 at factory level 1), not the shipped catalog (Assembler unlocks at factory level 3, slots unlock with levels). The exact violated invariant is not named in the diagnostic and was not investigated further (read-only role).
* Consequently the script numbers are **not valid measurements**: the three "starts" of 30, 30 and 28 ms (median 30 ms) are the time to the rejection path, with `reconciled from W003-S: False`, `gen 0` unchanged and the factory not producing. They do **not** show that the 2 s budget (PE-2) holds. The script's "median PASS" line is a false positive and is recorded here as UNRESOLVED.
* FPS capture (profiler-free, 0 Autoconnect Profiler lines, same process): control-1 (fresh run) 60.552 fps (3528 frames, 6 dropped), "producing" 59.711 fps (3484 frames, 14 dropped), control-2 53.045 fps (3109 frames, 49 dropped), controls mean 56.8 fps. The "producing" capture ran against the blocked start-up screen, so **it is not a producing-factory figure**; the budget (>= 60 FPS average) is UNRESOLVED. The two controls differ by 7.5 fps on a fresh empty run, which indicates the emulator noise level of this host (display refresh 60.000004 Hz).
* `F003LogScan`: 0 crash signatures in 11,513 lines; 30 persistence diagnostic lines, all from the rejected set above; 42 `E Unity` lines outside the known benign probes, all `return.blocked`.

To close this item, a W003-S (or equivalent 12-machine) checkpoint that the shipped catalog accepts is needed (fixture/catalog decision, a new Issue); then rerun `-Steps Preflight,W003Push,OfflineReturnTiming,ProducingFps,F003LogScan`. Pushing the set also leaves the device without a playable save: the player was reset with `pm clear` afterwards.

## T139 items deferred to device evidence

| Item | Result | Evidence |
|---|---|---|
| Android Back as Cancel (Remove confirmation open, `adb shell input keyevent 4`) | **PASS** | `s4.png` (open) -> `s5.png`: dialog closed, same PID 3017, the Miner and cash 15 unchanged, focus ring on the Remove button, activity stays resumed (`IdleFactoryGameActivity`) |
| OS back **gesture** | **UNRESOLVED** | The emulator uses the system navigation mode of the AVD; a gesture swipe was not injected. `KEYCODE_BACK` is what the gesture delivers to the activity on API 36 predictive-back apps without a callback; this is not a gesture test. |
| Real rotation with an open dialog, incl. notice and focus return | **PASS** | Remove confirmation: see above (notice "Cancelled by rotation", focus returns to Remove). The "Welcome back" summary dialog stayed open across a portrait to landscape rotation with focus on Continue (`cut-portrait.png`, `cut-land.png`). |
| Display cutout | **PASS (emulated)** | The default AVD has none (no cutout in `dumpsys display`). With overlay `com.android.internal.display.cutout.emulation.double` enabled, the portrait HUD sits below the cutout (top inset about 70 px at 1080 wide) and in landscape the HUD and tab bar are inset from both sides; nothing is hidden behind the cut (`cut-portrait.png`, `cut-land.png`). The overlay was disabled again afterwards. |

## T191 device profile (emulator)

| Item | Value |
|---|---|
| Detected input | touch (`android.hardware.touchscreen` present; Android is a mobile platform, so `ShellDeviceProfile.Detect` returns Touch) |
| Screen | `adb shell wm size` 1080x2400; `wm density` 420 dpi (Android reports 420; `Screen.dpi` is not logged by the build and was not read in the player) |
| Physical size | 1080 / 420 x 25.4 = 65.3 mm x 145.1 mm (portrait) |
| Required panel scale | 420 x 7 / 25.4 / 44 x 1.0001 = 2.631 px/pt, so panel points 410 x 912 (portrait) and 912 x 410 (landscape) |
| Layout class, portrait | CompactPortrait by `FactoryHudDocument.Classify` (min side 410 < 700); screenshots show the bottom tab bar (`s2.png`) |
| Layout class, landscape | CompactLandscape (912 x 410); screenshots show the side sheet with the bottom tab bar (`remove-rotated-landscape.png`) |
| Measured 44 pt target | Pixel scan of `s2.png`: `Build Miner` button 116 px high, `Close` button 116 x 116 px (44 pt x 2.631 = 115.8 px). 116 / 420 x 25.4 = **7.02 mm, at least 7 mm: PASS** |
| System font scale 100 % | `settings get system font_scale` 1.0: `s2.png`, single-row tab bar, no clipping |
| System font scale 200 % | `settings put system font_scale 2.0` (verified 2.0, app restarted): `f200-2.png` shows larger text, the tab bar stacked on two rows, the sheet scrolling, labels not truncated (the `Close` label fills its button tightly). Restored to 1.0 afterwards. |

## Unresolved and findings for the parent

1. **Blocker (new Issue needed):** the committed device set `Fixtures/SaveV2/device/w003-s` is rejected by the shipped catalog with `save.v2.invariant`. SC-008 return time on the emulator, the producing-factory FPS budget and the Prestige confirmation rotation remain unmeasured.
2. **Review item:** a rotation (focus cycle) calls `RunLifecycleAdapter.TryResume` and logs `ResumeAndReconcile`; the script asserts the opposite. Decide whether the script assertion or the lifecycle is wrong.
3. **Script follow-ups (no product change):** `TouchBuild` and the Remove "nothing spent" check cannot read a checkpoint that is only written on a lifecycle event; `OfflineReturnTiming` reports a median PASS when every start failed to reconcile; `ProducingFps` does not check that the producing capture ran on a loaded run.
4. OS back gesture not injected in the first run; injected in the rerun and FAILED (see "Rerun on T245 sets"). Everything is emulator-only.

## Rerun on T245 sets (2026-10-08)

**Emulator only**, same APK (SHA-256 `0597F48B...A47A9BA`, verified before the run; no product code changed since the build). The branch contains the merge of origin/main `45d23f3` (T245: device sets `w003-s-shipped` and `prestige-ready`, corrected `scripts/android-emulator-journey.ps1`). The emulator was killed (`adb emu kill`) and cold-booted again with `-gpu host -no-snapshot-load -no-boot-anim` for this rerun; system font scale 1.0, no cutout overlay, 1080x2400 at 420 dpi. Script reports: `journey-runE.md` to `journey-runH.md`; screenshots `rerun-*.png`.

### Eight-hour return and producing FPS (run E)

```
& scripts/android-emulator-journey.ps1 -Scenario F003 -Apk test-results/t124/f003-dev.apk -LeaseOwner issue-206 -Steps Preflight,Install,W003Push,OfflineReturnTiming,ProducingFps,F003LogScan
```

| Check | Verdict | Raw value |
|---|---|---|
| W003Push `w003-s-shipped` | PASS | device SHA-256 equals the committed `d62511d0...` (data) and `88b3cc96...` (manifest) |
| Departure beyond the 8 h cap | PASS | 25.414 h before the device clock, so each start reconciles exactly the capped 8 h |
| Start 1 / 2 / 3 `ResumeAndReconcile` | PASS | **761 / 745 / 746 ms**, all 3 accepted ("reconciled from w003-s-shipped"), 12 machines, cash 5,436,286 |
| **Median of 3 vs <= 2000 ms (PE-2, SC-008)** | **PASS** | **746 ms** (37 percent of the budget) |
| Production continues (PE-6) | PASS | gen 1 cash 5,436,286 -> gen 2 cash 5,437,930, 12 machines |
| ProducingFps: pushed run loaded before capture | PASS | reconciled in 745 ms from the set |
| **Producing average FPS vs >= 60 (budget 4, owner decision 2026-09-29)** | **PASS** | **61.145 fps**, 3563 frames, 6 dropped, 60 s |
| Same-session control (context) | INFO | control-1 61.458 fps (3578 frames, 3 dropped), control-2 61.561 fps (3583 frames, 2 dropped), mean 61.5; producing is 0.36 fps below the control mean |
| Profiler-free build | PASS | 0 Autoconnect Profiler lines in the producing capture |
| F003LogScan | PASS | 0 crash signatures in 35,679 lines; 0 coded diagnostics; 7 `E Unity` lines, all known benign probes |

Reading: the first run's FPS figures (59.7, controls 60.6 and 53.0) came from the blocked start-up screen and a long-running emulator; on a fresh cold boot and a loaded 12-machine factory the average is above 60 and equals the control within noise. Note for the timing: the set's Item effect (120 s) and Overdrive (30 s) expire within the first minutes of the 8 h credit, so most of the credited time runs at base speed; the 746 ms covers the whole 8 h reconcile of this set (a different workload from the W003-B event bound that missed on Windows in T122). The emulator figure is not a statement about physical devices.

### Prestige confirmation rotation (manual, set `prestige-ready`)

Run F pushed the set (`-Steps Preflight,W003Push -DeviceSet prestige-ready`: device SHA-256 `22184ca0...` data and `857ca112...` manifest PASS), then the player was launched by hand. The script step `PrestigeConfirmRotation` was **not usable**: its taps cannot scroll, and the sheet's `Sell the factory` button is below the fold in portrait (`rerun-p2.png` shows "Ready to sell", Total Factory Value 21.1M of 10.0M needed; `rerun-p3.png` after two upward swipes shows the button). Steps, all by `adb shell input`:

1. Cold start shows the "Welcome back" summary (8 h credited, `rerun-p1.png`); tap `Continue` (922,1772).
2. Prestige tab (990,2328), swipe `540 1900 -> 540 1000` twice, tap `Sell the factory` (196,2160): the "Sell the factory?" confirmation opens with focus on `Keep playing` (`rerun-p4.png`).
3. `cmd window user-rotation lock 1` (landscape, `ROTATION_90`, 2400x1080) while the dialog is open.

| Check | Verdict | Evidence |
|---|---|---|
| Rotated to landscape with the confirmation open | PASS | `ROTATION_90 2400x1080` |
| Dialog cancelled, notice shown, focus returned | **PASS** | `rerun-p5.png`: notice "Cancelled by rotation", dialog closed, focus ring on `Sell the factory` in the side sheet |
| Same process | PASS | PID 4679 before and after |
| Nothing spent | PASS (screenshots) | cash 17.8M before and after, 12 machines, run still 1, no sale; no checkpoint was pulled for this manual run |

### Rotation and Back gesture (run H, fresh run with one Miner)

```
& scripts/android-emulator-journey.ps1 -Scenario F003 -Apk test-results/t124/f003-dev.apk -LeaseOwner issue-206 -Steps Preflight,Install,Launch,TouchBuild,RemoveConfirmRotation,BackGesture -BuildTaps @('922,1352','306,786','528,1853') -RemoveTaps @('922,1352','540,2172')
```

The new forced departure checkpoint (HOME and return) shows a "Welcome back" summary (less than 1 s away), so the first tap of each tap list is `Continue` at (922,1352). Run G without it built nothing (0 machines, cash 25).

| Check | Verdict | Raw value |
|---|---|---|
| TouchBuild: baseline departure checkpoint | **FAIL** | `none` right after `pm clear` and first launch: the forced HOME/return did not produce a checkpoint yet |
| TouchBuild: machine built by touch | **FAIL (script), built** | `none -> gen 3, run 1, cash 15, 1 machine`; the FAIL is the missing baseline, the build itself is proven by the checkpoint and screenshots |
| RemoveConfirmRotation: baseline checkpoint | PASS | gen 5, cash 15, 1 machine |
| Rotated to landscape | PASS | `ROTATION_90 2400x1080` |
| Same process | PASS | PID 5770 |
| Return reconcile (R20, allowed) | INFO | 1 `ResumeAndReconcile` line, 32 ms |
| No coded diagnostics during the rotation | PASS | no `return.blocked`, `save.v2.*` or persistence line |
| Offline summary withheld | PASS (screenshots) | `rerun-remove-rotated.png`: notice "Cancelled by rotation", no Welcome back dialog |
| Nothing spent | **PASS** | gen 5 cash 15, 1 machine -> gen 9 cash 15, 1 machine |
| BackGesture: gestural navigation | PASS | `settings get secure navigation_mode` 2 (gestural) |
| BackGesture: same process, activity resumed, nothing spent | PASS | PID 5770; resumed `IdleFactoryGameActivity`; gen 13 -> gen 15, cash 15, 1 machine |
| **BackGesture: dialog closed (decisive, by screenshot)** | **FAIL** | `rerun-backgesture-open.png` and `rerun-backgesture-after.png`: after the script's edge swipe (`input swipe 0 1200 432 1200 150`) the "Remove Miner?" dialog **is still open** and the system bars appeared (clock, gesture handle). Two more manual swipes (`2 1200 500 1200 250`, `0 1200 700 1200 120`; screenshot `rerun-g3.png`) left it open too. The script's "nothing spent" and "activity resumed" PASS lines are therefore not evidence of a Cancel. |
| KEYCODE_BACK control, same dialog | PASS | `adb shell input keyevent 4` closed it, focus on Remove (`rerun-g4.png`); a "Welcome back" summary followed, caused by the focus cycle of the earlier gestures |

Interpretation: with gesture navigation the injected edge swipe only revealed the transient system bars and did not reach the app as a Back event, so "OS back gesture as Cancel" is **not met on the emulator**. An `adb input swipe` is not a hardware gesture, so this is an emulator-injection result, not a physical-device claim; whether the Player handles the gesture correctly (an `OnBackInvokedCallback`, or immersive mode swallowing the first edge swipe) has to be decided by the Architect / UI/UX lens and, if needed, checked on a physical Android device. It is recorded as a miss, not tuned away.

### Updated unresolved list

1. OS back gesture does not cancel the Remove confirmation on the emulator (above); needs a decision and possibly a physical-device check.
2. Script follow-ups: `TouchBuild` has no baseline checkpoint on a fresh `pm clear`; `PrestigeConfirmRotation` cannot scroll to `Sell the factory` (no swipe support), so the confirmation is evidenced manually; the forced departure checkpoint shows a "Welcome back" summary that the tap lists must dismiss first.
3. Everything here is emulator-only; no physical Android and no iOS claim (iOS deferred, Issue #39).
