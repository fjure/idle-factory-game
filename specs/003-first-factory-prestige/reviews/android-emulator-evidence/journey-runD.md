# Android emulator journey - 2026-10-08 21:38

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

| Step | Check | Result | Detail |
|---|---|---|---|
| Environment | emulator lease | **INFO** | emulator: renewed for 'issue-206' (81 min) |
| Preflight | device booted | **PASS** | emulator-5554 |
| Preflight | expected AVD | **PASS** | Medium_Phone_API_36.0, API 36 |
| Preflight | arm64-v8a via translation | **PASS** | x86_64,arm64-v8a; bridge libndk_translation.so |
| Preflight | GLES renderer | **INFO** | Google (NVIDIA Corporation), Android Emulator OpenGL ES Translator (NVIDIA GeForce RTX 5070 Ti/PCIe/SSE2), OpenGL ES 3.1 (4.5.0 NVIDIA 591.86) |
| W003Push | committed run-checkpoint.data-a.json | **PASS** | SHA-256 0467d293d69ed358 |
| W003Push | committed run-checkpoint.manifest-a.bin | **PASS** | SHA-256 84cfe0b4e7275743 |
| W003Push | device run-checkpoint.data-a.json | **PASS** | SHA-256 0467d293d69ed358 on the device |
| W003Push | device run-checkpoint.manifest-a.bin | **PASS** | SHA-256 84cfe0b4e7275743 on the device |
| OfflineReturnTiming | departure beyond the eight-hour cap | **PASS** | fixture departure is 25.413 h before the device clock, so each start reconciles exactly the capped 8 h |
| OfflineReturnTiming | start 1 | **FAIL** | 30 ms; reconciled from W003-S: False; gen 0, id 22222222, run 1, cash 1000, 12 machine(s), prestige 0, data 0467d293d69e |
| OfflineReturnTiming | start 2 | **FAIL** | 30 ms; reconciled from W003-S: False; gen 0, id 22222222, run 1, cash 1000, 12 machine(s), prestige 0, data 0467d293d69e |
| OfflineReturnTiming | start 3 | **FAIL** | 28 ms; reconciled from W003-S: False; gen 0, id 22222222, run 1, cash 1000, 12 machine(s), prestige 0, data 0467d293d69e |
| OfflineReturnTiming | median of starts | **PASS** | median 30 ms of [30, 30, 28] (budget <= 2000 ms, PE-2) |
| OfflineReturnTiming | W003-S keeps producing | **FAIL** | gen 0, id 22222222, run 1, cash 1000, 12 machine(s), prestige 0, data 0467d293d69e -> gen 0, id 22222222, run 1, cash 1000, 12 machine(s), prestige 0, data 0467d293d69e |
| ProducingFps | control-1 capture | **INFO** | 3528 frames, 6 dropped, 60.552 fps over 60 s; same process True |
| ProducingFps | producing capture | **INFO** | 3484 frames, 14 dropped, 59.711 fps over 60 s; same process True |
| ProducingFps | control-2 capture | **INFO** | 3109 frames, 49 dropped, 53.045 fps over 60 s; same process True |
| ProducingFps | profiler-free build | **PASS** | 0 Autoconnect Profiler line(s) in the producing capture (the frame-rate build must not autoconnect the profiler) |
| ProducingFps | producing average FPS | **FAIL** | 59,711 fps (budget >= 60, owner decision 2026-09-29) |
| ProducingFps | same-session control (context) | **INFO** | fresh-run control mean 56.8 fps (controls: 60,552, 53,045); producing 59.711 fps |
| F003LogScan | no crash signatures | **PASS** | 0 FATAL/F DEBUG/SIGSEGV line(s) in 11513 |
| F003LogScan | no coded diagnostics | **FAIL** | 30 persistence/start-up diagnostic line(s) |
| F003LogScan | E Unity lines | **INFO** | 50, of which 42 outside the known benign AssetPackManager and Autoconnect Profiler probes |
| F003LogScan | unexpected E Unity line | **INFO** | 10-08 19:37:38.263  6059  6077 E Unity   : return.blocked: The saved factory could not be loaded. Retry or start a new run. |
| F003LogScan | unexpected E Unity line | **INFO** | 10-08 19:37:38.263  6059  6077 E Unity   : UnityEngine.DebugLogHandler:Internal_Log(LogType, LogOption, String, Object) |
| F003LogScan | unexpected E Unity line | **INFO** | 10-08 19:37:38.263  6059  6077 E Unity   : IdleFactory.Unity.Bootstrap.CoreGameFoundationBootstrap:DisableWithError(String) |
| F003LogScan | unexpected E Unity line | **INFO** | 10-08 19:37:38.263  6059  6077 E Unity   : IdleFactory.Unity.Bootstrap.CoreGameFoundationBootstrap:DisableWithCode(String, String) |
| F003LogScan | unexpected E Unity line | **INFO** | 10-08 19:37:38.263  6059  6077 E Unity   : IdleFactory.Unity.Bootstrap.CoreGameFoundationBootstrap:InitializeRunRuntime(RunCatalog) |

Result: FAIL (6 check(s)). Raw evidence: this directory.
