# Android emulator journey - 2026-10-08 22:26

**Emulator only**: development evidence for this AVD and its ARM64 translation layer. No physical-device, performance-qualification or release claim.

| Environment | Value |
|---|---|
| Serial | `emulator-5554` |
| Avd | `Medium_Phone_API_36.0` |
| Fingerprint | `google/sdk_gphone64_x86_64/emu64xa:16/BP22.250325.006/13344233:user/release-keys` |
| Api | `36` |
| Abis | `x86_64,arm64-v8a` |
| NativeBridge | `libndk_translation.so` |
| Gles | `Google (NVIDIA Corporation), Android Emulator OpenGL ES Translator (NVIDIA GeForce RTX 5070 Ti/PCIe/SSE2), OpenGL ES 3.1 (4.5.0 NVIDIA 591.86)` |
| Display | `1080x2400` |
| Apk | `C:\Users\Domenik\ai-projects\idle-factory-game\.maqa-worktrees\issue-206\test-results\t124\f003-dev.apk (36972397 bytes, SHA-256 0597f48b3951c51b04f83d15c4d7107d009257de216c9f82a620f8894a47a9ba)` |

| Step | Check | Result | Detail |
|---|---|---|---|
| Environment | emulator lease | **INFO** | emulator: renewed for 'issue-206' (81 min) |
| Preflight | device booted | **PASS** | emulator-5554 |
| Preflight | expected AVD | **PASS** | Medium_Phone_API_36.0, API 36 |
| Preflight | arm64-v8a via translation | **PASS** | x86_64,arm64-v8a; bridge libndk_translation.so |
| Preflight | GLES renderer | **INFO** | Google (NVIDIA Corporation), Android Emulator OpenGL ES Translator (NVIDIA GeForce RTX 5070 Ti/PCIe/SSE2), OpenGL ES 3.1 (4.5.0 NVIDIA 591.86) |
| Install | adb install -r | **PASS** | Success |
| Install | pm clear | **PASS** | Success |
| W003Push | committed w003-s-shipped/run-checkpoint.data-a.json | **PASS** | SHA-256 d62511d042a63075 |
| W003Push | committed w003-s-shipped/run-checkpoint.manifest-a.bin | **PASS** | SHA-256 88b3cc964198b7a4 |
| W003Push | device w003-s-shipped/run-checkpoint.data-a.json | **PASS** | SHA-256 d62511d042a63075 on the device |
| W003Push | device w003-s-shipped/run-checkpoint.manifest-a.bin | **PASS** | SHA-256 88b3cc964198b7a4 on the device |
| OfflineReturnTiming | departure beyond the eight-hour cap | **PASS** | w003-s-shipped departure is 25.414 h before the device clock, so each accepted start reconciles exactly the capped 8 h |
| OfflineReturnTiming | start 1 | **PASS** | 761 ms; accepted, reconciled from w003-s-shipped; gen 1, id 408e2564, run 1, cash 5436286, 12 machine(s), prestige 0, data bdf2f0bc46ff |
| OfflineReturnTiming | start 2 | **PASS** | 745 ms; accepted, reconciled from w003-s-shipped; gen 1, id 306a506f, run 1, cash 5436286, 12 machine(s), prestige 0, data f9565bd54fb5 |
| OfflineReturnTiming | start 3 | **PASS** | 746 ms; accepted, reconciled from w003-s-shipped; gen 1, id a2712c58, run 1, cash 5436286, 12 machine(s), prestige 0, data 1f296bf528b7 |
| OfflineReturnTiming | median of accepted starts | **PASS** | median 746 ms of [761, 745, 746] accepted (3 of 3 starts accepted; budget <= 2000 ms, PE-2) |
| OfflineReturnTiming | w003-s-shipped keeps producing | **PASS** | gen 1, id a2712c58, run 1, cash 5436286, 12 machine(s), prestige 0, data 1f296bf528b7 -> gen 2, id dd4b3732, run 1, cash 5437930, 12 machine(s), prestige 0, data 2c8f6c42eb78 |
| ProducingFps | control-1 capture | **INFO** | 3578 frames, 3 dropped, 61.458 fps over 60 s; same process True |
| ProducingFps | producing pushed run loaded | **PASS** | reconciled in 745 ms from 22222222-2222-2222-2222-000000000030; gen 1, id 7de606c7, run 1, cash 5436286, 12 machine(s), prestige 0, data 187862dd5fd7 |
| ProducingFps | producing capture | **INFO** | 3563 frames, 6 dropped, 61.145 fps over 60 s; same process True |
| ProducingFps | control-2 capture | **INFO** | 3583 frames, 2 dropped, 61.561 fps over 60 s; same process True |
| ProducingFps | profiler-free build | **PASS** | 0 Autoconnect Profiler line(s) in the producing capture (the frame-rate build must not autoconnect the profiler) |
| ProducingFps | producing average FPS | **PASS** | 61,145 fps (budget >= 60, owner decision 2026-09-29) |
| ProducingFps | same-session control (context) | **INFO** | fresh-run control mean 61.5 fps (controls: 61,458, 61,561); producing 61.145 fps |
| F003LogScan | no crash signatures | **PASS** | 0 FATAL/F DEBUG/SIGSEGV line(s) in 35679 |
| F003LogScan | no coded diagnostics | **PASS** | 0 persistence/start-up diagnostic line(s) |
| F003LogScan | E Unity lines | **INFO** | 7, of which 0 outside the known benign AssetPackManager and Autoconnect Profiler probes |

Result: PASS. Raw evidence: this directory.
