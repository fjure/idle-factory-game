# Android emulator journey - 2026-10-27 10:07

**Emulator only**: development evidence for this AVD and its ARM64 translation layer. No physical-device, performance-qualification or release claim.

| Environment | Value |
|---|---|
| Serial | `emulator-5554` |
| Avd | `Medium_Phone_API_36.0` |
| Fingerprint | `google/sdk_gphone64_x86_64/emu64xa:16/BP22.250325.006/13344233:user/release-keys` |
| Api | `36` |
| Abis | `x86_64,arm64-v8a` |
| NativeBridge | `libndk_translation.so` |
| Gles | `Google (NVIDIA Corporation), Android Emulator OpenGL ES Translator (NVIDIA GeForce RTX 5070 Ti/PCIe/SSE2), OpenGL ES 3.1 (4.5.0 NVIDIA 610.88)` |
| Display | `1080x2400` |
| Apk | `C:\Users\Domenik\ai-projects\idle-factory-game\.maqa-worktrees\issue-416-t351\test-results\android\t351\t351-gles3.apk (37333555 bytes, SHA-256 57d519052105c8d16bc51e68133bb00824626a061068704e4c648e2da4be2a33)` |
| UnityBuild | `Development, il2cpp, arm64-v8a` |

| Step | Check | Result | Detail |
|---|---|---|---|
| Environment | emulator lease | **INFO** | emulator: renewed for 'issue-416' (99 min) |
| Preflight | device booted | **PASS** | emulator-5554 |
| Preflight | expected AVD | **PASS** | Medium_Phone_API_36.0, API 36 |
| Preflight | arm64-v8a via translation | **PASS** | x86_64,arm64-v8a; bridge libndk_translation.so |
| Preflight | GLES renderer | **INFO** | Google (NVIDIA Corporation), Android Emulator OpenGL ES Translator (NVIDIA GeForce RTX 5070 Ti/PCIe/SSE2), OpenGL ES 3.1 (4.5.0 NVIDIA 610.88) |
| Install | /data free space | **PASS** | 720 MB free (88 percent used), floor 400 MB |
| Install | adb install -r | **PASS** | Success |
| Install | pm clear | **PASS** | Success |
| Launch | process started | **PASS** | PID 11651 |
| Launch | time to graphics device | **INFO** | 2 s after process start |
| Launch | primary ABI | **PASS** | arm64-v8a |
| Launch | Unity build | **INFO** | Development, il2cpp, arm64-v8a |
| Launch | graphics device | **PASS** | Creating OpenGL ES 3.1 graphics device ; Context level  <OpenGL ES 3.1> ; Context handle 1692685248 |
| Launch | time to first display | **INFO** | 1s554ms |
| Launch | display | **INFO** | ROTATION_0 1080x2400, requested SCREEN_ORIENTATION_FULL_USER |
| Launch | screenshot | **INFO** | 01-launch.png |
| W005Push | committed w005-l/run-checkpoint.data-a.json | **PASS** | SHA-256 7066eed15513d2bd |
| W005Push | committed w005-l/run-checkpoint.manifest-a.bin | **PASS** | SHA-256 8c9d93d12a81c466 |
| W005Push | device w005-l/run-checkpoint.data-a.json | **PASS** | SHA-256 7066eed15513d2bd on the device |
| W005Push | device w005-l/run-checkpoint.manifest-a.bin | **PASS** | SHA-256 8c9d93d12a81c466 on the device |
| F005Sell | W005-L loaded | **PASS** | reconciled from 22222222-2222-2222-2222-000000000050 after 8 s; gen 1, id a34970e2, run 3, cash 289457264, 12 machine(s), prestige 2, data f078f102977e |
| F005Sell | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005Sell | screenshot (after Welcome back) | **INFO** | 02-f005-start.png |
| F005Sell | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005Sell | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005Sell | screenshot (Inventory card before the sale) | **INFO** | 03-f005-sell-before.png |
| F005Sell | Sell button located | **PASS** | primary button bands at y fractions: 0,732 |
| F005Sell | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005Sell | screenshot (sold line) | **INFO** | 04-f005-sell-after.png |
| F005Sell | process alive | **PASS** | PID 11786 |
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
| F005SessionLog | screenshot (Session log section) | **INFO** | 08-f005-log-section.png |
| F005SessionLog | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005SessionLog | screenshot (after Export tap 1) | **INFO** | 09-f005-log-after-tap1.png |
| F005SessionLog | UI ready before the first tap | **INFO** | surface present after 2 s |
| F005SessionLog | screenshot (in-app Last export path; compare with the pulled path) | **INFO** | 10-f005-log-exported.png |
| F005SessionLog | export file created | **PASS** | 1 new file(s) in Exports/: session-log-20261027T090857Z.jsonl |
| F005SessionLog | adb pull of the export | **PASS** | /sdcard/Android/data/com.DefaultCompany.idlefactorygame/files/IdleFactory/Exports/session-log-20261027T090857Z.jsonl: 1 file pulled, 0 skipped. 0.7 MB/s (1014 bytes in 0.001s) |
| F005SessionLog | paths | **INFO** | in-app path (see the screenshot): /storage/emulated/0/Android/data/com.DefaultCompany.idlefactorygame/files/IdleFactory/Exports/session-log-20261027T090857Z.jsonl; pulled from /sdcard/Android/data/com.DefaultCompany.idlefactorygame/files/IdleFactory/Exports/session-log-20261027T090857Z.jsonl to C:\Users\Domenik\ai-projects\idle-factory-game\.maqa-worktrees\issue-416-t351\test-results\android\t351\journey\export\session-log-20261027T090857Z.jsonl (1014 bytes) |
| F005SessionLog | export is non-empty, one JSON object per line with t and crc | **PASS** | 7 line(s), 1014 bytes, 0 malformed |
| F005SessionLog | export contains sale-manual-any | **PASS** | events: header x1, lifecycle x1, contract-autoDeliver-contract.gear x1, offlineReturn x1, sale-manual-iron.bar x1, sale-manual-any x1, contract-accept-contract.timed-bar x1, contract-autoDeliver-contract.timed-bar x1 |
| F005SessionLog | export contains contract-accept-contract.timed-bar | **PASS** | events: header x1, lifecycle x1, contract-autoDeliver-contract.gear x1, offlineReturn x1, sale-manual-iron.bar x1, sale-manual-any x1, contract-accept-contract.timed-bar x1, contract-autoDeliver-contract.timed-bar x1 |
| F005SessionLog | export contains contract-autoDeliver-contract.timed-bar | **PASS** | events: header x1, lifecycle x1, contract-autoDeliver-contract.gear x1, offlineReturn x1, sale-manual-iron.bar x1, sale-manual-any x1, contract-accept-contract.timed-bar x1, contract-autoDeliver-contract.timed-bar x1 |
| F005SessionLog | no file path in the log (SEC-1) | **PASS** | 0 line(s) with a path |
| Home | departure checkpoint written | **PASS** | gen 1, id a34970e2, run 3, cash 289457264, 12 machine(s), prestige 2, data f078f102977e -> gen 2, id 3f669b78, run 3, cash 290487647, 12 machine(s), prestige 2, data 2113a6f75130 |
| Home | return in the same process | **PASS** | PID 11786 -> 11786 |
| Home | return checkpoint written | **PASS** | gen 3, id 0588bf17, run 3, cash 290608991, 12 machine(s), prestige 2, data ddc59a8d0109 |
| Home | screenshot (offline return summary) | **INFO** | 11-home-return.png |
| Lock | device asleep | **PASS** | mWakefulness=Asleep |
| Lock | departure checkpoint written | **PASS** | gen 4, id 415a9133, run 3, cash 290650703, 12 machine(s), prestige 2, data abbf0b78d9f2 |
| Lock | awake in the same process | **PASS** | awake=True, PID 11786 -> 11786 |
| Lock | return checkpoint written | **PASS** | gen 5, id a514b96a, run 3, cash 290760671, 12 machine(s), prestige 2, data 47d95059fb79 |
| Lock | screenshot | **INFO** | 12-lock-return.png |
| ColdRestart | process stopped | **PASS** | PID 11786 gone |
| ColdRestart | new process | **PASS** | PID 11786 -> 12482 |
| ColdRestart | time to graphics device | **INFO** | 2 s |
| ColdRestart | reconciled from the last durable checkpoint | **PASS** | gen 6, id 20c47071, run 3, cash 290865109, 12 machine(s), prestige 2, data 4d12d37e6e4c |
| ColdRestart | machine set restored | **PASS** | 12 -> 12 machine(s) |
| ColdRestart | screenshot (restored factory before any input) | **INFO** | 13-cold-restart.png |
| W005LReturn | departure beyond the eight-hour cap | **PASS** | w005-l departure is 25.859 h before the device clock, so each accepted start reconciles exactly the capped 8 h |
| W005LReturn | start 1 | **PASS** | 815 ms; accepted, reconciled from w005-l; gen 1, id 4198dd69, run 3, cash 289457264, 12 machine(s), prestige 2, data 79a3d4fe1319 |
| W005LReturn | start 2 | **PASS** | 791 ms; accepted, reconciled from w005-l; gen 1, id 5680c825, run 3, cash 289457264, 12 machine(s), prestige 2, data 0edba46ba4f2 |
| W005LReturn | start 3 | **PASS** | 813 ms; accepted, reconciled from w005-l; gen 1, id 64c2fdbe, run 3, cash 289457264, 12 machine(s), prestige 2, data 3f25e5dbeda3 |
| W005LReturn | start 4 | **PASS** | 811 ms; accepted, reconciled from w005-l; gen 1, id 99ddf74a, run 3, cash 289457264, 12 machine(s), prestige 2, data 21cffc334f26 |
| W005LReturn | start 5 | **PASS** | 825 ms; accepted, reconciled from w005-l; gen 1, id e8a8c92d, run 3, cash 289457264, 12 machine(s), prestige 2, data 293f5f1e7041 |
| W005LReturn | median of accepted starts | **PASS** | median 813 ms of [815, 791, 813, 811, 825] accepted (5 of 5 starts accepted; budget <= 2000 ms, PE-2) |
| W005LReturn | w005-l keeps producing | **PASS** | gen 1, id e8a8c92d, run 3, cash 289457264, 12 machine(s), prestige 2, data 293f5f1e7041 -> gen 2, id cffba902, run 3, cash 289728234, 12 machine(s), prestige 2, data b9fc1be2b5a0 |
| ProducingFps | control-1 capture | **INFO** | 3542 frames, 8 dropped, 60.797 fps over 60 s; same process True |
| ProducingFps | producing pushed run loaded | **PASS** | reconciled in 822 ms from 22222222-2222-2222-2222-000000000050; gen 1, id 46814da8, run 3, cash 289457264, 12 machine(s), prestige 2, data d8e339f717d0 |
| ProducingFps | producing capture | **INFO** | 3528 frames, 8 dropped, 60.586 fps over 60 s; same process True |
| ProducingFps | control-2 capture | **INFO** | 3540 frames, 10 dropped, 60.810 fps over 60 s; same process True |
| ProducingFps | profiler-free build | **PASS** | 0 Autoconnect Profiler line(s) in the producing capture (the frame-rate build must not autoconnect the profiler) |
| ProducingFps | producing average FPS | **PASS** | 60,586 fps (budget >= 60, owner decision 2026-09-29) |
| ProducingFps | same-session control (context) | **INFO** | fresh-run control mean 60.8 fps (controls: 60,797, 60,81); producing 60.586 fps |
| F005LogScan | no crash signatures | **PASS** | 0 FATAL/F DEBUG/SIGSEGV line(s) in 13191 |
| F005LogScan | no coded diagnostics | **PASS** | 0 persistence/start-up diagnostic line(s) |
| F005LogScan | E Unity lines | **INFO** | 12, of which 0 outside the known benign AssetPackManager and Autoconnect Profiler probes |

Result: PASS. Raw evidence: this directory.
