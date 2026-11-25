# Android emulator journey - 2026-11-25 00:58

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
| Install | /data free space | **PASS** | 3605 MB free (54 percent used), floor 400 MB |
| Install | adb install -r | **PASS** | Success |
| Install | pm clear | **PASS** | Success |
| Launch | process started | **PASS** | PID 19416 |
| Launch | time to graphics device | **INFO** | 2 s after process start |
| Launch | first scene loaded (playable shell) | **PASS** | 4 s after the graphics device |
| Launch | primary ABI | **PASS** | arm64-v8a |
| Launch | Unity build | **INFO** | Development, il2cpp, arm64-v8a |
| Launch | graphics device | **PASS** | Creating OpenGL ES 3.1 graphics device ; Context level  <OpenGL ES 3.1> ; Context handle -747146224 |
| Launch | time to first display | **INFO** | 1s336ms |
| Launch | display | **INFO** | ROTATION_0 1080x2400, requested SCREEN_ORIENTATION_FULL_USER |
| Launch | screenshot | **INFO** | 01-launch.png |
| W005Push | committed w005-l/run-checkpoint.data-a.json | **PASS** | SHA-256 7066eed15513d2bd |
| W005Push | committed w005-l/run-checkpoint.manifest-a.bin | **PASS** | SHA-256 8c9d93d12a81c466 |
| W005Push | device w005-l/run-checkpoint.data-a.json | **PASS** | SHA-256 7066eed15513d2bd on the device |
| W005Push | device w005-l/run-checkpoint.manifest-a.bin | **PASS** | SHA-256 8c9d93d12a81c466 on the device |
| F005Sell | W005-L loaded | **PASS** | reconciled from 22222222-2222-2222-2222-000000000050 after 8 s; gen 1, id 4669c7f5, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data 9eca868bdd51 |
| F005Sell | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005Sell | screenshot (after Welcome back) | **INFO** | 02-f005-start.png |
| F005Sell | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005Sell | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005Sell | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005Sell | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005Sell | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005Sell | screenshot (Inventory card before the sale) | **INFO** | 03-f005-sell-before.png |
| F005Sell | Sell button located | **PASS** | primary button bands at y fractions: 0,617 |
| F005Sell | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005Sell | screenshot (sold line) | **INFO** | 04-f005-sell-after.png |
| F005Sell | process alive | **PASS** | PID 19561 |
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
| F005SessionLog | export file created | **PASS** | 1 new file(s) in Exports/: session-log-20261125T000027Z.jsonl |
| F005SessionLog | adb pull of the export | **PASS** | /sdcard/Android/data/com.DefaultCompany.idlefactorygame/files/IdleFactory/Exports/session-log-20261125T000027Z.jsonl: 1 file pulled, 0 skipped. 0.6 MB/s (1177 bytes in 0.002s) |
| F005SessionLog | paths | **INFO** | in-app path (see the screenshot): /storage/emulated/0/Android/data/com.DefaultCompany.idlefactorygame/files/IdleFactory/Exports/session-log-20261125T000027Z.jsonl; pulled from /sdcard/Android/data/com.DefaultCompany.idlefactorygame/files/IdleFactory/Exports/session-log-20261125T000027Z.jsonl to C:\Users\Domenik\ai-projects\idle-factory-game\.maqa-worktrees\issue-714-t542-final-qa\specs\006-world-first-factory-experience\reviews\final-test-evidence\journey-f005-run2\export\session-log-20261125T000027Z.jsonl (1177 bytes) |
| F005SessionLog | export is non-empty, one JSON object per line with t and crc | **PASS** | 8 line(s), 1177 bytes, 0 malformed |
| F005SessionLog | export contains sale-manual-any | **FAIL** | events: header x2, lifecycle x2, contract-autoDeliver-contract.gear x1, offlineReturn x1, contract-accept-contract.timed-bar x1, contract-autoDeliver-contract.timed-bar x1 |
| F005SessionLog | export contains contract-accept-contract.timed-bar | **PASS** | events: header x2, lifecycle x2, contract-autoDeliver-contract.gear x1, offlineReturn x1, contract-accept-contract.timed-bar x1, contract-autoDeliver-contract.timed-bar x1 |
| F005SessionLog | export contains contract-autoDeliver-contract.timed-bar | **PASS** | events: header x2, lifecycle x2, contract-autoDeliver-contract.gear x1, offlineReturn x1, contract-accept-contract.timed-bar x1, contract-autoDeliver-contract.timed-bar x1 |
| F005SessionLog | no file path in the log (SEC-1) | **PASS** | 0 line(s) with a path |
| Home | departure checkpoint written | **PASS** | gen 6, id 4653c868, run 3, cash 289907467, 12 machine(s), levels 670, prestige 2, data 672aaec48a0a -> gen 7, id ff98070a, run 3, cash 289907467, 12 machine(s), levels 670, prestige 2, data ae8c94b248ed |
| Home | return in the same process | **PASS** | PID 19561 -> 19561 |
| Home | return checkpoint written | **PASS** | gen 8, id d896fffa, run 3, cash 289907467, 12 machine(s), levels 670, prestige 2, data dec955cb7ba6 |
| Home | screenshot (offline return summary) | **INFO** | 11-home-return.png |
| Lock | device asleep | **PASS** | mWakefulness=Asleep |
| Lock | departure checkpoint written | **PASS** | gen 9, id c97f0cc8, run 3, cash 289907467, 12 machine(s), levels 670, prestige 2, data 8b36ed696af6 |
| Lock | awake in the same process | **PASS** | awake=True, PID 19561 -> 19561 |
| Lock | return checkpoint written | **PASS** | gen 10, id 78f31a96, run 3, cash 289907467, 12 machine(s), levels 670, prestige 2, data 2cf0449258fa |
| Lock | screenshot | **INFO** | 12-lock-return.png |
| ColdRestart | process stopped | **PASS** | PID 19561 gone |
| ColdRestart | new process | **PASS** | PID 19561 -> 20405 |
| ColdRestart | time to graphics device | **INFO** | 2 s |
| ColdRestart | first scene loaded (playable shell) | **PASS** | 4 s after the graphics device |
| ColdRestart | reconciled from the last durable checkpoint | **PASS** | gen 11, id 9cb9868f, run 3, cash 289907467, 12 machine(s), levels 670, prestige 2, data 38ee63984977 |
| ColdRestart | machine set restored | **PASS** | 12 -> 12 machine(s) |
| ColdRestart | screenshot (restored factory before any input) | **INFO** | 13-cold-restart.png |
| W005LReturn | departure beyond the eight-hour cap | **PASS** | w005-l departure is 26.546 h before the device clock, so each accepted start reconciles exactly the capped 8 h |
| W005LReturn | start 1 | **PASS** | 784 ms; accepted, reconciled from w005-l; gen 1, id e76e33a5, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data f6767641b4e1 |
| W005LReturn | start 2 | **PASS** | 812 ms; accepted, reconciled from w005-l; gen 1, id b1e52ecd, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data b7a0f08873c0 |
| W005LReturn | start 3 | **PASS** | 799 ms; accepted, reconciled from w005-l; gen 1, id c04ac23b, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data b3707e328173 |
| W005LReturn | start 4 | **PASS** | 787 ms; accepted, reconciled from w005-l; gen 1, id 71b57c4c, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data c5fab14f6ddf |
| W005LReturn | start 5 | **PASS** | 787 ms; accepted, reconciled from w005-l; gen 1, id c30aed8d, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data 34848ac8c875 |
| W005LReturn | median of accepted starts | **PASS** | median 787 ms of [784, 812, 799, 787, 787] accepted (5 of 5 starts accepted; budget <= 2000 ms, PE-2) |
| W005LReturn | w005-l keeps producing | **PASS** | gen 1, id c30aed8d, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data 34848ac8c875 -> gen 2, id 95510cd2, run 3, cash 289726496, 12 machine(s), levels 670, prestige 2, data c12118e74e79 |
| ProducingFps | control-1 capture | **INFO** | 2300 frames, 36 dropped, 38.920 fps over 60 s; same process True |
| ProducingFps | producing pushed run loaded | **PASS** | reconciled in 798 ms from 22222222-2222-2222-2222-000000000050; gen 1, id bffc1077, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data da526f9638fd |
| ProducingFps | producing capture | **INFO** | 2331 frames, 52 dropped, 39.442 fps over 60 s; same process True |
| ProducingFps | control-2 capture | **INFO** | 2331 frames, 55 dropped, 39.429 fps over 60 s; same process True |
| ProducingFps | profiler-free build | **PASS** | 0 Autoconnect Profiler line(s) in the producing capture (the frame-rate build must not autoconnect the profiler) |
| ProducingFps | producing average FPS | **FAIL** | 39,442 fps (budget >= 60, owner decision 2026-09-29) |
| ProducingFps | same-session control (context) | **INFO** | fresh-run control mean 39.2 fps (controls: 38,92, 39,429); producing 39.442 fps |
| F005LogScan | no crash signatures | **PASS** | 0 FATAL/F DEBUG/SIGSEGV line(s) in 13382 |
| F005LogScan | no coded diagnostics | **PASS** | 0 persistence/start-up diagnostic line(s) |
| F005LogScan | E Unity lines | **INFO** | 12, of which 0 outside the known benign AssetPackManager and Autoconnect Profiler probes |

Result: FAIL (2 check(s)). Raw evidence: this directory.
