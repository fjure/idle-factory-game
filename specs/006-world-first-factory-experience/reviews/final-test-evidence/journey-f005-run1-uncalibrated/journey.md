# Android emulator journey - 2026-11-25 00:41

**Emulator only**: development evidence for this AVD and its ARM64 translation layer. No physical-device, performance-qualification or release claim.

| Environment | Value |
|---|---|
| Serial | `emulator-5554` |
| Avd | `IdleFactory_Test_API_36` |
| Fingerprint | `google/sdk_gphone64_x86_64/emu64xa:16/BP22.250325.006/13344233:user/release-keys` |
| Api | `36` |
| Abis | `x86_64,arm64-v8a` |
| NativeBridge | `libndk_translation.so` |
| Gles | `Google (NVIDIA Corporation), Android Emulator OpenGL ES Translator (NVIDIA GeForce RTX 5070 Ti/PCIe/SSE2), OpenGL ES 3.1 (4.5.0 NVIDIA 621.01)` |
| Display | `1080x2400` |
| Apk | `C:\Users\Domenik\AppData\Local\Temp\t542\t542.apk (46722286 bytes, SHA-256 62dbdb293f522fed2b7c63b87683028e188a991a11bb638577ef3ae2960d7c5f)` |
| UnityBuild | `Development, il2cpp, arm64-v8a` |

| Step | Check | Result | Detail |
|---|---|---|---|
| Environment | emulator lease | **INFO** | emulator: renewed for 'issue-714' (99 min) |
| Preflight | device booted | **PASS** | emulator-5554 |
| Preflight | expected AVD | **PASS** | IdleFactory_Test_API_36, API 36 |
| Preflight | arm64-v8a via translation | **PASS** | x86_64,arm64-v8a; bridge libndk_translation.so |
| Preflight | GLES renderer | **INFO** | Google (NVIDIA Corporation), Android Emulator OpenGL ES Translator (NVIDIA GeForce RTX 5070 Ti/PCIe/SSE2), OpenGL ES 3.1 (4.5.0 NVIDIA 621.01) |
| Install | /data free space | **PASS** | 3606 MB free (54 percent used), floor 400 MB |
| Install | adb install -r | **PASS** | Success |
| Install | pm clear | **PASS** | Success |
| Launch | process started | **PASS** | PID 14787 |
| Launch | time to graphics device | **INFO** | 2 s after process start |
| Launch | first scene loaded (playable shell) | **PASS** | 4 s after the graphics device |
| Launch | primary ABI | **PASS** | arm64-v8a |
| Launch | Unity build | **INFO** | Development, il2cpp, arm64-v8a |
| Launch | graphics device | **PASS** | Creating OpenGL ES 3.1 graphics device ; Context level  <OpenGL ES 3.1> ; Context handle -747121744 |
| Launch | time to first display | **INFO** | 1s446ms |
| Launch | display | **INFO** | ROTATION_0 1080x2400, requested SCREEN_ORIENTATION_FULL_USER |
| Launch | screenshot | **INFO** | 01-launch.png |
| W005Push | committed w005-l/run-checkpoint.data-a.json | **PASS** | SHA-256 7066eed15513d2bd |
| W005Push | committed w005-l/run-checkpoint.manifest-a.bin | **PASS** | SHA-256 8c9d93d12a81c466 |
| W005Push | device w005-l/run-checkpoint.data-a.json | **PASS** | SHA-256 7066eed15513d2bd on the device |
| W005Push | device w005-l/run-checkpoint.manifest-a.bin | **PASS** | SHA-256 8c9d93d12a81c466 on the device |
| F005Sell | W005-L loaded | **PASS** | reconciled from 22222222-2222-2222-2222-000000000050 after 8 s; gen 1, id def0d534, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data d638c9289641 |
| F005Sell | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005Sell | screenshot (after Welcome back) | **INFO** | 02-f005-start.png |
| F005Sell | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005Sell | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005Sell | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005Sell | screenshot (Inventory card before the sale) | **INFO** | 03-f005-sell-before.png |
| F005Sell | Sell button located | **PASS** | primary button bands at y fractions: 0,713 |
| F005Sell | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005Sell | screenshot (sold line) | **INFO** | 04-f005-sell-after.png |
| F005Sell | process alive | **PASS** | PID 14931 |
| F005AutoDeliver | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005AutoDeliver | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005AutoDeliver | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005AutoDeliver | screenshot (offers) | **INFO** | 05-f005-contract-offers.png |
| F005AutoDeliver | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005AutoDeliver | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005AutoDeliver | screenshot (active contract, Auto-deliver Off) | **INFO** | 06-f005-contract-active.png |
| F005AutoDeliver | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005AutoDeliver | screenshot (after the toggle: auto-delivered line) | **INFO** | 07-f005-contract-autodeliver.png |
| F005SessionLog | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005SessionLog | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005SessionLog | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005SessionLog | screenshot (Session log section) | **INFO** | 08-f005-log-section.png |
| F005SessionLog | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005SessionLog | screenshot (after Export tap 1) | **INFO** | 09-f005-log-after-tap1.png |
| F005SessionLog | Export tap 1 | **INFO** | no new export after the tap at 0.489,0.798; retrying |
| F005SessionLog | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005SessionLog | screenshot (after Export tap 2) | **INFO** | 10-f005-log-after-tap2.png |
| F005SessionLog | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005SessionLog | screenshot (in-app Last export path; compare with the pulled path) | **INFO** | 11-f005-log-exported.png |
| F005SessionLog | export file created | **PASS** | 1 new file(s) in Exports/: session-log-20261124T234326Z.jsonl |
| F005SessionLog | adb pull of the export | **PASS** | /sdcard/Android/data/com.DefaultCompany.idlefactorygame/files/IdleFactory/Exports/session-log-20261124T234326Z.jsonl: 1 file pulled, 0 skipped. 0.8 MB/s (1054 bytes in 0.001s) |
| F005SessionLog | paths | **INFO** | in-app path (see the screenshot): /storage/emulated/0/Android/data/com.DefaultCompany.idlefactorygame/files/IdleFactory/Exports/session-log-20261124T234326Z.jsonl; pulled from /sdcard/Android/data/com.DefaultCompany.idlefactorygame/files/IdleFactory/Exports/session-log-20261124T234326Z.jsonl to C:\Users\Domenik\ai-projects\idle-factory-game\.maqa-worktrees\issue-714-t542-final-qa\specs\006-world-first-factory-experience\reviews\final-test-evidence\journey-f005-run1\export\session-log-20261124T234326Z.jsonl (1054 bytes) |
| F005SessionLog | export is non-empty, one JSON object per line with t and crc | **PASS** | 8 line(s), 1054 bytes, 0 malformed |
| F005SessionLog | export contains sale-manual-any | **PASS** | events: header x2, lifecycle x3, contract-autoDeliver-contract.gear x1, offlineReturn x1, sale-manual-iron.bar x1, sale-manual-any x1 |
| F005SessionLog | export contains contract-accept-contract.timed-bar | **FAIL** | events: header x2, lifecycle x3, contract-autoDeliver-contract.gear x1, offlineReturn x1, sale-manual-iron.bar x1, sale-manual-any x1 |
| F005SessionLog | export contains contract-autoDeliver-contract.timed-bar | **FAIL** | events: header x2, lifecycle x3, contract-autoDeliver-contract.gear x1, offlineReturn x1, sale-manual-iron.bar x1, sale-manual-any x1 |
| F005SessionLog | no file path in the log (SEC-1) | **PASS** | 0 line(s) with a path |
| Home | departure checkpoint written | **PASS** | gen 4, id 851fa608, run 3, cash 290585230, 12 machine(s), levels 670, prestige 2, data 1d9206229c28 -> gen 5, id 83c28c07, run 3, cash 290740702, 12 machine(s), levels 670, prestige 2, data 5683b035aaf1 |
| Home | return in the same process | **PASS** | PID 14931 -> 14931 |
| Home | return checkpoint written | **PASS** | gen 6, id a5099068, run 3, cash 290858254, 12 machine(s), levels 670, prestige 2, data 81f7df2aa3d1 |
| Home | screenshot (offline return summary) | **INFO** | 12-home-return.png |
| Lock | device asleep | **PASS** | mWakefulness=Asleep |
| Lock | departure checkpoint written | **PASS** | gen 7, id 22503541, run 3, cash 290899966, 12 machine(s), levels 670, prestige 2, data b2445228f736 |
| Lock | awake in the same process | **PASS** | awake=True, PID 14931 -> 14931 |
| Lock | return checkpoint written | **PASS** | gen 8, id ca4b9126, run 3, cash 291006142, 12 machine(s), levels 670, prestige 2, data c151cdbb7cf6 |
| Lock | screenshot | **INFO** | 13-lock-return.png |
| ColdRestart | process stopped | **PASS** | PID 14931 gone |
| ColdRestart | new process | **PASS** | PID 14931 -> 15754 |
| ColdRestart | time to graphics device | **INFO** | 2 s |
| ColdRestart | first scene loaded (playable shell) | **PASS** | 4 s after the graphics device |
| ColdRestart | reconciled from the last durable checkpoint | **PASS** | gen 9, id 688b1f76, run 3, cash 291114372, 12 machine(s), levels 670, prestige 2, data f0911e3f3f03 |
| ColdRestart | machine set restored | **PASS** | 12 -> 12 machine(s) |
| ColdRestart | screenshot (restored factory before any input) | **INFO** | 14-cold-restart.png |
| W005LReturn | departure beyond the eight-hour cap | **PASS** | w005-l departure is 26.545 h before the device clock, so each accepted start reconciles exactly the capped 8 h |
| W005LReturn | start 1 | **PASS** | 793 ms; accepted, reconciled from w005-l; gen 1, id db484643, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data e20aff9be4e3 |
| W005LReturn | start 2 | **PASS** | 840 ms; accepted, reconciled from w005-l; gen 1, id d600615a, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data f071ab2e85cc |
| W005LReturn | start 3 | **PASS** | 787 ms; accepted, reconciled from w005-l; gen 1, id d1724320, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data 91d52e282b6f |
| W005LReturn | start 4 | **PASS** | 791 ms; accepted, reconciled from w005-l; gen 1, id ea194da5, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data 24c8e792247a |
| W005LReturn | start 5 | **PASS** | 794 ms; accepted, reconciled from w005-l; gen 1, id 02508be1, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data 99b823d4b782 |
| W005LReturn | median of accepted starts | **PASS** | median 793 ms of [793, 840, 787, 791, 794] accepted (5 of 5 starts accepted; budget <= 2000 ms, PE-2) |
| W005LReturn | w005-l keeps producing | **PASS** | gen 1, id 02508be1, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data 99b823d4b782 -> gen 2, id d8edc754, run 3, cash 289724442, 12 machine(s), levels 670, prestige 2, data 7bc71d6d8ed2 |
| ProducingFps | control-1 capture | **INFO** | 2381 frames, 54 dropped, 40.335 fps over 60 s; same process True |
| ProducingFps | producing pushed run loaded | **PASS** | reconciled in 798 ms from 22222222-2222-2222-2222-000000000050; gen 1, id ad944382, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data 7600f64755ec |
| ProducingFps | producing capture | **INFO** | 2365 frames, 50 dropped, 40.028 fps over 60 s; same process True |
| ProducingFps | control-2 capture | **INFO** | 2376 frames, 63 dropped, 40.296 fps over 60 s; same process True |
| ProducingFps | profiler-free build | **PASS** | 0 Autoconnect Profiler line(s) in the producing capture (the frame-rate build must not autoconnect the profiler) |
| ProducingFps | producing average FPS | **FAIL** | 40,028 fps (budget >= 60, owner decision 2026-09-29) |
| ProducingFps | same-session control (context) | **INFO** | fresh-run control mean 40.3 fps (controls: 40,335, 40,296); producing 40.028 fps |
| F005LogScan | no crash signatures | **PASS** | 0 FATAL/F DEBUG/SIGSEGV line(s) in 13501 |
| F005LogScan | no coded diagnostics | **PASS** | 0 persistence/start-up diagnostic line(s) |
| F005LogScan | E Unity lines | **INFO** | 12, of which 0 outside the known benign AssetPackManager and Autoconnect Profiler probes |

Result: FAIL (3 check(s)). Raw evidence: this directory.
