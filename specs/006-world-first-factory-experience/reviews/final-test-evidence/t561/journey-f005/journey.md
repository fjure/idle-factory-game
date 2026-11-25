# Android emulator journey - 2026-11-25 12:25

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
| Apk | `C:\Users\Domenik\AppData\Local\Temp\t561\t561.apk (46722918 bytes, SHA-256 71cbaa6eefb83e537645ffee5c108382daa122f146e932b11509de5956de463d)` |
| UnityBuild | `Development, il2cpp, arm64-v8a` |

| Step | Check | Result | Detail |
|---|---|---|---|
| Environment | emulator lease | **INFO** | emulator: renewed for 'issue-782' (99 min) |
| Preflight | device booted | **PASS** | emulator-5554 |
| Preflight | expected AVD | **PASS** | IdleFactory_Test_API_36, API 36 |
| Preflight | arm64-v8a via translation | **PASS** | x86_64,arm64-v8a; bridge libndk_translation.so |
| Preflight | GLES renderer | **INFO** | Google (NVIDIA Corporation), Android Emulator OpenGL ES Translator (NVIDIA GeForce RTX 5070 Ti/PCIe/SSE2), OpenGL ES 3.1 (4.5.0 NVIDIA 621.01) |
| Install | /data free space | **PASS** | 3486 MB free (56 percent used), floor 400 MB |
| Install | adb install -r | **PASS** | Success |
| Install | pm clear | **PASS** | Success |
| Launch | process started | **PASS** | PID 14051 |
| Launch | time to graphics device | **INFO** | 2 s after process start |
| Launch | first scene loaded (playable shell) | **PASS** | 4 s after the graphics device |
| Launch | primary ABI | **PASS** | arm64-v8a |
| Launch | Unity build | **INFO** | Development, il2cpp, arm64-v8a |
| Launch | graphics device | **PASS** | Creating OpenGL ES 3.1 graphics device ; Context level  <OpenGL ES 3.1> ; Context handle -2091506048 |
| Launch | time to first display | **INFO** | 1s351ms |
| Launch | display | **INFO** | ROTATION_0 1080x2400, requested SCREEN_ORIENTATION_FULL_USER |
| Launch | screenshot | **INFO** | 01-launch.png |
| W005Push | committed w005-l/run-checkpoint.data-a.json | **PASS** | SHA-256 7066eed15513d2bd |
| W005Push | committed w005-l/run-checkpoint.manifest-a.bin | **PASS** | SHA-256 8c9d93d12a81c466 |
| W005Push | device w005-l/run-checkpoint.data-a.json | **PASS** | SHA-256 7066eed15513d2bd on the device |
| W005Push | device w005-l/run-checkpoint.manifest-a.bin | **PASS** | SHA-256 8c9d93d12a81c466 on the device |
| F005Sell | W005-L loaded | **PASS** | reconciled from 22222222-2222-2222-2222-000000000050 after 10 s; gen 1, id d8303714, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data 005546bd890f |
| F005Sell | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005Sell | screenshot (after Welcome back) | **INFO** | 02-f005-start.png |
| F005Sell | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005Sell | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005Sell | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005Sell | screenshot (Inventory card before the sale) | **INFO** | 03-f005-sell-before.png |
| F005Sell | Sell button located | **PASS** | primary button bands at y fractions: 0,710 |
| F005Sell | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005Sell | screenshot (sold line) | **INFO** | 04-f005-sell-after.png |
| F005Sell | process alive | **PASS** | PID 14197 |
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
| F005SessionLog | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005SessionLog | screenshot (in-app Last export path; compare with the pulled path) | **INFO** | 10-f005-log-exported.png |
| F005SessionLog | export file created | **PASS** | 1 new file(s) in Exports/: session-log-20261125T112703Z.jsonl |
| F005SessionLog | adb pull of the export | **PASS** | /sdcard/Android/data/com.DefaultCompany.idlefactorygame/files/IdleFactory/Exports/session-log-20261125T112703Z.jsonl: 1 file pulled, 0 skipped. 0.1 MB/s (1270 bytes in 0.009s) |
| F005SessionLog | paths | **INFO** | in-app path (see the screenshot): /storage/emulated/0/Android/data/com.DefaultCompany.idlefactorygame/files/IdleFactory/Exports/session-log-20261125T112703Z.jsonl; pulled from /sdcard/Android/data/com.DefaultCompany.idlefactorygame/files/IdleFactory/Exports/session-log-20261125T112703Z.jsonl to C:\Users\Domenik\ai-projects\idle-factory-game\.maqa-worktrees\issue-782-t561-t542-findings\test-results\android\r-F005\export\session-log-20261125T112703Z.jsonl (1270 bytes) |
| F005SessionLog | export is non-empty, one JSON object per line with t and crc | **PASS** | 9 line(s), 1270 bytes, 0 malformed |
| F005SessionLog | export contains sale-manual-any | **PASS** | events: header x2, lifecycle x2, contract-autoDeliver-contract.gear x1, offlineReturn x1, sale-manual-iron.bar x1, sale-manual-any x1, contract-accept-contract.timed-bar x1, contract-autoDeliver-contract.timed-bar x1 |
| F005SessionLog | export contains contract-accept-contract.timed-bar | **PASS** | events: header x2, lifecycle x2, contract-autoDeliver-contract.gear x1, offlineReturn x1, sale-manual-iron.bar x1, sale-manual-any x1, contract-accept-contract.timed-bar x1, contract-autoDeliver-contract.timed-bar x1 |
| F005SessionLog | export contains contract-autoDeliver-contract.timed-bar | **PASS** | events: header x2, lifecycle x2, contract-autoDeliver-contract.gear x1, offlineReturn x1, sale-manual-iron.bar x1, sale-manual-any x1, contract-accept-contract.timed-bar x1, contract-autoDeliver-contract.timed-bar x1 |
| F005SessionLog | no file path in the log (SEC-1) | **PASS** | 0 line(s) with a path |
| Home | departure checkpoint written | **PASS** | gen 5, id 492f67fe, run 3, cash 290529359, 12 machine(s), levels 670, prestige 2, data 8bbddd82cabe -> gen 6, id 54677e99, run 3, cash 290646911, 12 machine(s), levels 670, prestige 2, data 9cb91ac292f3 |
| Home | return in the same process | **PASS** | PID 14197 -> 14197 |
| Home | return checkpoint written | **PASS** | gen 7, id e25424d4, run 3, cash 290766517, 12 machine(s), levels 670, prestige 2, data e13521d47f64 |
| Home | screenshot (offline return summary) | **INFO** | 11-home-return.png |
| Lock | device asleep | **PASS** | mWakefulness=Asleep |
| Lock | departure checkpoint written | **PASS** | gen 8, id 2b320779, run 3, cash 290821343, 12 machine(s), levels 670, prestige 2, data d8e620e5bda8 |
| Lock | awake in the same process | **PASS** | awake=True, PID 14197 -> 14197 |
| Lock | return checkpoint written | **PASS** | gen 9, id 00361793, run 3, cash 290927519, 12 machine(s), levels 670, prestige 2, data b16b139c5c95 |
| Lock | screenshot | **INFO** | 12-lock-return.png |
| ColdRestart | process stopped | **PASS** | PID 14197 gone |
| ColdRestart | new process | **PASS** | PID 14197 -> 15024 |
| ColdRestart | time to graphics device | **INFO** | 2 s |
| ColdRestart | first scene loaded (playable shell) | **PASS** | 4 s after the graphics device |
| ColdRestart | reconciled from the last durable checkpoint | **PASS** | gen 10, id 156e879b, run 3, cash 291033695, 12 machine(s), levels 670, prestige 2, data a04831f20a85 |
| ColdRestart | machine set restored | **PASS** | 12 -> 12 machine(s) |
| ColdRestart | screenshot (restored factory before any input) | **INFO** | 13-cold-restart.png |
| W005LReturn | departure beyond the eight-hour cap | **PASS** | w005-l departure is 26.557 h before the device clock, so each accepted start reconciles exactly the capped 8 h |
| W005LReturn | start 1 | **PASS** | 788 ms; accepted, reconciled from w005-l; gen 1, id ce5dd22b, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data d30e305439eb |
| W005LReturn | start 2 | **PASS** | 804 ms; accepted, reconciled from w005-l; gen 1, id a0974024, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data f947dc9fb50f |
| W005LReturn | start 3 | **PASS** | 814 ms; accepted, reconciled from w005-l; gen 1, id f5e01635, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data 5d313d1e432f |
| W005LReturn | start 4 | **PASS** | 789 ms; accepted, reconciled from w005-l; gen 1, id 053fc696, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data f2d47129fee1 |
| W005LReturn | start 5 | **PASS** | 794 ms; accepted, reconciled from w005-l; gen 1, id 906d6d96, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data 8953601f0ba3 |
| W005LReturn | median of accepted starts | **PASS** | median 794 ms of [788, 804, 814, 789, 794] accepted (5 of 5 starts accepted; budget <= 2000 ms, PE-2) |
| W005LReturn | w005-l keeps producing | **PASS** | gen 1, id 906d6d96, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data 8953601f0ba3 -> gen 2, id 3316e052, run 3, cash 289724442, 12 machine(s), levels 670, prestige 2, data 00b97e2cdd13 |
| ProducingFps | control-1 capture | **INFO** | 2352 frames, 49 dropped, 39.821 fps over 60 s; same process True |
| ProducingFps | producing pushed run loaded | **PASS** | reconciled in 836 ms from 22222222-2222-2222-2222-000000000050; gen 1, id b3268d15, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data d4f4bc7253b1 |
| ProducingFps | producing capture | **INFO** | 2376 frames, 50 dropped, 40.257 fps over 60 s; same process True |
| ProducingFps | control-2 capture | **INFO** | 2367 frames, 61 dropped, 40.105 fps over 60 s; same process True |
| ProducingFps | profiler-free build | **PASS** | 0 Autoconnect Profiler line(s) in the producing capture (the frame-rate build must not autoconnect the profiler) |
| ProducingFps | producing average FPS | **FAIL** | 40,257 fps (budget >= 60, owner decision 2026-09-29) |
| ProducingFps | same-session control (context) | **INFO** | fresh-run control mean 40 fps (controls: 39,821, 40,105); producing 40.257 fps |
| F005LogScan | no crash signatures | **PASS** | 0 FATAL/F DEBUG/SIGSEGV line(s) in 13925 |
| F005LogScan | no coded diagnostics | **PASS** | 0 persistence/start-up diagnostic line(s) |
| F005LogScan | E Unity lines | **INFO** | 12, of which 0 outside the known benign AssetPackManager and Autoconnect Profiler probes |

Result: FAIL (1 check(s)). Raw evidence: this directory.
