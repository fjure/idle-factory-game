# Android emulator journey - 2026-11-25 01:07

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
| Launch | process started | **PASS** | PID 21751 |
| Launch | time to graphics device | **INFO** | 2 s after process start |
| Launch | first scene loaded (playable shell) | **PASS** | 4 s after the graphics device |
| Launch | primary ABI | **PASS** | arm64-v8a |
| Launch | Unity build | **INFO** | Development, il2cpp, arm64-v8a |
| Launch | graphics device | **PASS** | Creating OpenGL ES 3.1 graphics device ; Context level  <OpenGL ES 3.1> ; Context handle -747118576 |
| Launch | time to first display | **INFO** | 1s308ms |
| Launch | display | **INFO** | ROTATION_0 1080x2400, requested SCREEN_ORIENTATION_FULL_USER |
| Launch | screenshot | **INFO** | 01-launch.png |
| W005Push | committed w005-l/run-checkpoint.data-a.json | **PASS** | SHA-256 7066eed15513d2bd |
| W005Push | committed w005-l/run-checkpoint.manifest-a.bin | **PASS** | SHA-256 8c9d93d12a81c466 |
| W005Push | device w005-l/run-checkpoint.data-a.json | **PASS** | SHA-256 7066eed15513d2bd on the device |
| W005Push | device w005-l/run-checkpoint.manifest-a.bin | **PASS** | SHA-256 8c9d93d12a81c466 on the device |
| F005Sell | W005-L loaded | **PASS** | reconciled from 22222222-2222-2222-2222-000000000050 after 8 s; gen 1, id f5d2b3f8, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data 552a695c6b51 |
| F005Sell | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005Sell | screenshot (after Welcome back) | **INFO** | 02-f005-start.png |
| F005Sell | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005Sell | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005Sell | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005Sell | screenshot (Inventory card before the sale) | **INFO** | 03-f005-sell-before.png |
| F005Sell | Sell button located | **PASS** | primary button bands at y fractions: 0,723 |
| F005Sell | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005Sell | screenshot (sold line) | **INFO** | 04-f005-sell-after.png |
| F005Sell | process alive | **PASS** | PID 21898 |
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
| F005SessionLog | export file created | **PASS** | 1 new file(s) in Exports/: session-log-20261125T000912Z.jsonl |
| F005SessionLog | adb pull of the export | **PASS** | /sdcard/Android/data/com.DefaultCompany.idlefactorygame/files/IdleFactory/Exports/session-log-20261125T000912Z.jsonl: 1 file pulled, 0 skipped. 0.8 MB/s (1270 bytes in 0.001s) |
| F005SessionLog | paths | **INFO** | in-app path (see the screenshot): /storage/emulated/0/Android/data/com.DefaultCompany.idlefactorygame/files/IdleFactory/Exports/session-log-20261125T000912Z.jsonl; pulled from /sdcard/Android/data/com.DefaultCompany.idlefactorygame/files/IdleFactory/Exports/session-log-20261125T000912Z.jsonl to C:\Users\Domenik\ai-projects\idle-factory-game\.maqa-worktrees\issue-714-t542-final-qa\specs\006-world-first-factory-experience\reviews\final-test-evidence\journey-f005-run3-nofps\export\session-log-20261125T000912Z.jsonl (1270 bytes) |
| F005SessionLog | export is non-empty, one JSON object per line with t and crc | **PASS** | 9 line(s), 1270 bytes, 0 malformed |
| F005SessionLog | export contains sale-manual-any | **PASS** | events: header x2, lifecycle x2, contract-autoDeliver-contract.gear x1, offlineReturn x1, sale-manual-iron.bar x1, sale-manual-any x1, contract-accept-contract.timed-bar x1, contract-autoDeliver-contract.timed-bar x1 |
| F005SessionLog | export contains contract-accept-contract.timed-bar | **PASS** | events: header x2, lifecycle x2, contract-autoDeliver-contract.gear x1, offlineReturn x1, sale-manual-iron.bar x1, sale-manual-any x1, contract-accept-contract.timed-bar x1, contract-autoDeliver-contract.timed-bar x1 |
| F005SessionLog | export contains contract-autoDeliver-contract.timed-bar | **PASS** | events: header x2, lifecycle x2, contract-autoDeliver-contract.gear x1, offlineReturn x1, sale-manual-iron.bar x1, sale-manual-any x1, contract-accept-contract.timed-bar x1, contract-autoDeliver-contract.timed-bar x1 |
| F005SessionLog | no file path in the log (SEC-1) | **PASS** | 0 line(s) with a path |
| Home | departure checkpoint written | **PASS** | gen 5, id a74bf249, run 3, cash 290527621, 12 machine(s), levels 670, prestige 2, data cf407c592e08 -> gen 6, id 26d2a7a0, run 3, cash 290639327, 12 machine(s), levels 670, prestige 2, data baa7b0eeedb7 |
| Home | return in the same process | **PASS** | PID 21898 -> 21898 |
| Home | return checkpoint written | **PASS** | gen 7, id a6ab4391, run 3, cash 290756879, 12 machine(s), levels 670, prestige 2, data 69f1d512abb3 |
| Home | screenshot (offline return summary) | **INFO** | 11-home-return.png |
| Lock | device asleep | **PASS** | mWakefulness=Asleep |
| Lock | departure checkpoint written | **PASS** | gen 8, id 265a6704, run 3, cash 290815813, 12 machine(s), levels 670, prestige 2, data 0799e8dbe5cd |
| Lock | awake in the same process | **PASS** | awake=True, PID 21898 -> 21898 |
| Lock | return checkpoint written | **PASS** | gen 9, id 3e8c8643, run 3, cash 290923727, 12 machine(s), levels 670, prestige 2, data 5c896800ae5c |
| Lock | screenshot | **INFO** | 12-lock-return.png |
| ColdRestart | process stopped | **PASS** | PID 21898 gone |
| ColdRestart | new process | **PASS** | PID 21898 -> 22671 |
| ColdRestart | time to graphics device | **INFO** | 2 s |
| ColdRestart | first scene loaded (playable shell) | **PASS** | 4 s after the graphics device |
| ColdRestart | reconciled from the last durable checkpoint | **PASS** | gen 10, id 9b05eaec, run 3, cash 291037487, 12 machine(s), levels 670, prestige 2, data f84f6547b5b3 |
| ColdRestart | machine set restored | **PASS** | 12 -> 12 machine(s) |
| ColdRestart | screenshot (restored factory before any input) | **INFO** | 13-cold-restart.png |
| W005LReturn | departure beyond the eight-hour cap | **PASS** | w005-l departure is 26.546 h before the device clock, so each accepted start reconciles exactly the capped 8 h |
| W005LReturn | start 1 | **PASS** | 801 ms; accepted, reconciled from w005-l; gen 1, id fb36158f, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data a85cc0f92727 |
| W005LReturn | start 2 | **PASS** | 798 ms; accepted, reconciled from w005-l; gen 1, id e34725e8, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data 188847a2803c |
| W005LReturn | start 3 | **PASS** | 788 ms; accepted, reconciled from w005-l; gen 1, id b6e48772, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data 65cd5b5a371d |
| W005LReturn | start 4 | **PASS** | 784 ms; accepted, reconciled from w005-l; gen 1, id fb13e9d9, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data 8bb53473f818 |
| W005LReturn | start 5 | **PASS** | 805 ms; accepted, reconciled from w005-l; gen 1, id c9916c19, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data 33bbf57cb859 |
| W005LReturn | median of accepted starts | **PASS** | median 798 ms of [801, 798, 788, 784, 805] accepted (5 of 5 starts accepted; budget <= 2000 ms, PE-2) |
| W005LReturn | w005-l keeps producing | **PASS** | gen 1, id c9916c19, run 3, cash 289457264, 12 machine(s), levels 670, prestige 2, data 33bbf57cb859 -> gen 2, id 5ce12a9f, run 3, cash 289724442, 12 machine(s), levels 670, prestige 2, data 832097496970 |
| F005LogScan | no crash signatures | **PASS** | 0 FATAL/F DEBUG/SIGSEGV line(s) in 10139 |
| F005LogScan | no coded diagnostics | **PASS** | 0 persistence/start-up diagnostic line(s) |
| F005LogScan | E Unity lines | **INFO** | 9, of which 0 outside the known benign AssetPackManager and Autoconnect Profiler probes |

Result: PASS. Raw evidence: this directory.
