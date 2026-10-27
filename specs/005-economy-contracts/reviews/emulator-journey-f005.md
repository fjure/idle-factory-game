# F005 Android emulator journey (T351, #416)

**Emulator-only evidence.** Development evidence for this AVD and its ARM64 translation layer. It makes no physical-device, performance-qualification or release claim. Author: Test/QA (T351). Independent QA and DevOps review follow the commit.

## Run

| Item | Value |
|---|---|
| Date | 2026-10-27 (journey run 10:07 local) |
| Build commit | `bbb6d98` (origin/main after batches A to C, T348, T352; content revision 3) |
| Build | `IdleFactory.Editor.PlatformBuildCommands.BuildAndroidArm64DevelopmentGles3 -noProfilerAutoconnect`, batch mode in the worktree: Development, IL2CPP, ARM64, OpenGL ES 3 only, succeeded, 0 errors |
| APK | `test-results/android/t351/t351-gles3.apk`, 37,333,555 bytes, SHA-256 `57d519052105c8d16bc51e68133bb00824626a061068704e4c648e2da4be2a33` (installed by the journey) |
| AVD | `Medium_Phone_API_36.0`, API 36, x86_64 with `libndk_translation.so` (arm64-v8a), 1080x2400 portrait, `emulator-5554` |
| Boot mode | cold boot, host GPU: `emulator -avd Medium_Phone_API_36.0 -gpu host -no-snapshot-load -no-snapshot-save`, started from the main checkout directory. GLES renderer: NVIDIA GeForce RTX 5070 Ti translator, OpenGL ES 3.1 |
| Command | `powershell -NoProfile -ExecutionPolicy Bypass -Command "& ./scripts/android-emulator-journey.ps1 -Scenario F005 -Apk test-results/android/t351/t351-gles3.apk -OutDir test-results/android/t351/journey -LeaseOwner issue-416"` |
| Exit | 0, `Journey: PASS.` (no FAIL row) |
| Leases | `unity` and `emulator` held by the parent as `issue-416`; the script renewed the emulator lease |
| Host load (PF-9) | Not measured. The AVD, the journey and this session were the only workloads started by this assignment; no foreign `adb`/`monkey` process was present before the run. Other sessions, browsers and launchers on the workstation were not inventoried, so the timings carry that unrecorded deviation. |
| Build kind | Development build (not release): the `Development Console` and `Development Build` labels are visible in screenshots |

## Steps and results

| Step | Result | Evidence |
|---|---|---|
| Install | PASS | stale `com.UnityTestRunner.UnityTestRunner` removed earlier (see AVD cleanup), `/data` 720 MB free (88 percent used), install Success, `pm clear` |
| Launch | PASS | arm64-v8a, OpenGL ES 3.1 graphics device 2 s after process start, first display 1.55 s |
| W005Push | PASS | `01-w005-l.json` SHA-256 `7066eed15513d2bd...70fd` checked locally and on the device; derived manifest `8c9d93d12a81c466...` (slot A, sequence 1, binds the data by SHA-256) |
| F005Sell | PASS | W005-L accepted (reconciled from `22222222-...-000000000050`), Welcome back dismissed, manual Sell of Iron Bar: screenshot `04-f005-sell-after.png` shows "Sold 1 Iron Bar: +$4"; the export carries a manual `sale` of `iron.bar` |
| F005AutoDeliver | PASS | accepted the Timed bar offer (165 Iron Bar, in stock) and turned Auto-deliver on: screenshot `07-f005-contract-autodeliver.png` shows "Auto-delivered 165 Iron Bar: +1,009 Cash, +2 RP."; export lines `contract accept` and `contract autoDeliver` for `contract.timed-bar` |
| F005SessionLog | PASS | Export session log; in-app text "Exported 7 lines" and the path below; export pulled with `adb pull`, 7 lines, 1,014 bytes, 0 malformed (every line has `t` and `crc`); no file path inside the log (SEC-1) |
| Home | PASS | departure checkpoint written (gen 1 to 2), same process, return checkpoint (gen 3) |
| Lock | PASS | device asleep, departure checkpoint (gen 4), awake in the same process, return checkpoint (gen 5) |
| ColdRestart | PASS | new process, reconciled gen 6, 12 machines restored |
| W005LReturn | PASS | see below |
| W005LFps | PASS | see below |
| F005LogScan | PASS | 0 FATAL/F DEBUG/SIGSEGV in 13,191 lines; 0 coded diagnostics; 12 `E Unity` lines, all the known AssetPackManager and Autoconnect probes |

### Export path (in-app against pulled)

- Shown in-app (screenshot `10-f005-log-exported.png`, read by the author): `/storage/emulated/0/Android/data/com.DefaultCompany.idlefactorygame/files/IdleFactory/Exports/session-log-20261027T090857Z.jsonl`, followed by "On this device the file is copied with adb pull."
- Pulled: `/sdcard/Android/data/com.DefaultCompany.idlefactorygame/files/IdleFactory/Exports/session-log-20261027T090857Z.jsonl` (`/sdcard` is the same storage as `/storage/emulated/0`), 1,014 bytes. Same file name; the in-app line count (7) equals the pulled line count (7).
- The pulled export is committed under `specs/005-economy-contracts/reviews/emulator-journey-f005/`.

### W005-L return (8 h capped, content revision 3)

Five cold starts, each re-pushing the pair; `ResumeAndReconcile ms` from the Development-build log; every start accepted (no rejection diagnostic, checkpoint reconciled from the pushed id):

| Start | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|
| ms | 815 | 791 | 813 | 811 | 825 |

Median **813 ms**, range 791 to 825 ms, against the 2,000 ms emulator budget (plan, Technical Context): PASS. The set keeps producing afterwards (cash 289,457,264 to 289,728,234, generation 1 to 2). The Windows player gate is separate (500 ms).

### Frame rate

Profiler-free build, W005-L loaded between two fresh-run controls in the same session, 30 s warm-up, 60 s SurfaceFlinger capture each:

| Capture | Frames | Dropped | Average fps |
|---|---|---|---|
| control-1 (fresh run) | 3,542 | 8 | 60.797 |
| **W005-L producing** | 3,528 | 8 | **60.586** |
| control-2 (fresh run) | 3,540 | 10 | 60.810 |

Budget 60 average fps (owner decision 2026-09-29): PASS. The display on this AVD is vsync-limited to about 60 Hz, so the figure shows that W005-L does not drop below the cap, not headroom above it.

## Deviations, flaky points and fixes made to the script

1. **Checkpoint path.** Home, Lock and ColdRestart read `Checkpoints/v1`; they now read `Checkpoints/v2/run-checkpoint.*` (the reader the F003 steps already used). All their checks pass. The Profile step still reads the retired v1 layout (`factory-checkpoint.*`) and is not covered by T351.
2. **adb-created parent directories break the game.** A first attempt that created `IdleFactory/Checkpoints/v2` with `adb shell mkdir -p` before the app had ever run produced `persistence.failed: Checkpoint storage is unavailable.` (the app cannot write into directories owned by `shell`). After a launch (directories created by the app) the same push is accepted. The script pushes only after a launch; documented in its header.
3. **Fixed tap positions are not reliable inside a scrolled list.** The second complete run tapped the Auto-sell toggle instead of Sell (swipe fling distance varies) and the changed state made Auto-deliver miss. The Sell button is now located from the screenshot (full-width bright-cyan band) before a single tap; the Contracts and Session log taps are at the end of their lists, where the layout is deterministic. One earlier run also lost its Export tap (the Session log toggle flipped Off); the step now waits and retries once at the alternate layout and records a retry as INFO. The final run needed no retry. Cause of that one lost tap is not established.
4. **Welcome back summary.** W005-L reports "Away for 25,858 h 20 min. Credited: 8 h." and "Contract auto-delivered: 165 Gear. Reward: +9,780 Cash, +8 RP" on every cold start; the 8 h cap behaves as specified.
5. The W005-L manifest is derived by the script from the committed `w003-s-shipped` manifest (same 163-byte layout; the template's trailing SHA-256 recomputes); no Assets file was changed.
6. Taps are 1080x2400 fractions: the step set is specific to the portrait AVD screen class. Other screen classes, 200 percent text and accessibility are not covered by this evidence.

## Recorded open items (no code in T351)

| Item | Recommendation |
|---|---|
| PF-7 session-log flush calibration (`SESSIONLOG-FLUSH` against the 25 ms and 256-sample budget; Windows 3.7 to 5.5 ms at 140 samples) | Needs a GLES3 test-player path: add a build method that forces OpenGL ES 3 for the Android test player (or a DevTools probe) so the player no longer dies at start under translation. DevOps/Test, a new task after T351. |
| Android overlay smoke of the moved DevTools overlay (T340) | No build method produces DevTools plus GLES3 (`BuildAndroidArm64DevelopmentGles3` has no `IDLEFACTORY_DEV`; `...DevTools` uses Vulkan). Add a DevTools plus GLES3 variant through `extraScriptingDefines`, never ProjectSettings. |
| `scripts/check-release-build.ps1` gradleOut fallback (guard `$Apk` around `Resolve-RuleNodesFromGradleOut` when aapt2 is missing; doc note that `IdleFactoryBackupRules.androidlib/build.gradle` hard-codes compile/target SDK 36, build-tools 36.0.0, min SDK 26) | Belongs to T350 (#415), which runs after T351. |
| Profile step (T040) still reads `Checkpoints/v1` | Convert like Home/Lock if the exploratory profile is run again. |
| PF-9 host load | Record the workstation load for the next quiet-host timing; this run did not. |

## AVD cleanup

- `/data` was 88 percent used (about 713 MB free) before cleanup; the earlier failure was at 92 percent.
- Removed (adb uninstall): `com.UnityTestRunner.UnityTestRunner`, the Unity test-runner player of earlier test runs. About 230 MB were freed (85 percent used).
- Not touched: `de.hamm.habittracker` (a third-party app, not ours), system data and user data. After the final install `/data` held 720 MB free (88 percent used). `pm trim-caches` was run once (no effect).
- The script now removes only that known stale package of ours and fails below a 400 MB free-space floor, instead of wiping data.

## Raw artifacts

`specs/005-economy-contracts/reviews/emulator-journey-f005/`: `journey.md` (full step table), `session-log-20261027T090857Z.jsonl` (pulled export), and screenshots `04-f005-sell-after.png`, `07-f005-contract-autodeliver.png`, `10-f005-log-exported.png`. The complete run directory (`test-results/android/t351/journey/`, logcat, checkpoints, further screenshots) stays untracked.
