# Android emulator evidence (T124, Issue #206)

**Emulator only.** Every result below was measured on the Android Studio AVD `Medium_Phone_API_36.0` (API 36, x86_64 host with ARM64 translation, host GPU). No physical Android and no iOS claim (iOS deferred, Issue #39). Covers FR-033, SC-006, SC-007, SC-008.

**Verdict: PARTIAL, with one blocking finding.** The touch journey, the Remove confirmation, the T139 device items and the T191 device profile are recorded. The committed `W003-S` device checkpoint set **cannot be loaded by the real Player** (`save.v2.invariant`), so the eight-hour return time, the producing-factory FPS and the Prestige confirmation on `W003-S` are **UNRESOLVED**. No product code, fixture or scene was changed; the miss is recorded, not tuned away.

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

## W003-S eight-hour return time and producing-factory FPS: UNRESOLVED

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
4. OS back gesture not injected (above). Everything is emulator-only.
