# Android emulator journey - 2026-11-25 00:35

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
| W004VHash | `1920dfafb75e69131fe43bb38f66c088762b195c46aa7916c568747c7ecca6fc` |

| Step | Check | Result | Detail |
|---|---|---|---|
| Environment | emulator lease | **INFO** | emulator: renewed for 'issue-714' (77 min) |
| Preflight | device booted | **PASS** | emulator-5554 |
| Preflight | expected AVD | **PASS** | IdleFactory_Test_API_36, API 36 |
| Preflight | arm64-v8a via translation | **PASS** | x86_64,arm64-v8a; bridge libndk_translation.so |
| Preflight | GLES renderer | **INFO** | Google (NVIDIA Corporation), Android Emulator OpenGL ES Translator (NVIDIA GeForce RTX 5070 Ti/PCIe/SSE2), OpenGL ES 3.1 (4.5.0 NVIDIA 621.01) |
| Install | /data free space | **PASS** | 3606 MB free (54 percent used), floor 400 MB |
| Install | adb install -r | **PASS** | Success |
| Install | pm clear | **PASS** | Success |
| Launch | process started | **PASS** | PID 13427 |
| Launch | time to graphics device | **INFO** | 2 s after process start |
| Launch | first scene loaded (playable shell) | **PASS** | 4 s after the graphics device |
| Launch | primary ABI | **PASS** | arm64-v8a |
| Launch | Unity build | **INFO** | Development, il2cpp, arm64-v8a |
| Launch | graphics device | **PASS** | Creating OpenGL ES 3.1 graphics device ; Context level  <OpenGL ES 3.1> ; Context handle -747116992 |
| Launch | time to first display | **INFO** | 1s364ms |
| Launch | display | **INFO** | ROTATION_0 1080x2400, requested SCREEN_ORIENTATION_FULL_USER |
| Launch | screenshot | **INFO** | 01-launch.png |
| W004VPush | W004-V fixture present | **PASS** | Assets/IdleFactory/Tests/PlayMode/Fixtures/W004V/W004VFixture.cs |
| W004VPush | W004-V fixture hash recorded | **PASS** | ExpectedHash 1920dfafb75e69131fe43bb38f66c088762b195c46aa7916c568747c7ecca6fc |
| W004VPush | committed w003-s-shipped/run-checkpoint.data-a.json | **PASS** | SHA-256 4f7ed559fb1a396a |
| W004VPush | committed w003-s-shipped/run-checkpoint.manifest-a.bin | **PASS** | SHA-256 781810f0d39f3069 |
| W004VPush | device w003-s-shipped/run-checkpoint.data-a.json | **PASS** | SHA-256 4f7ed559fb1a396a on the device |
| W004VPush | device w003-s-shipped/run-checkpoint.manifest-a.bin | **PASS** | SHA-256 781810f0d39f3069 on the device |
| F004Restyled | W004-V base set loaded | **PASS** | reconciled from 22222222-2222-2222-2222-000000000030 after 8 s; gen 1, id 7a66cce9, run 1, cash 5681865, 12 machine(s), levels 331, prestige 0, data a712b9a93fa5 |
| F004Restyled | UI ready before the first tap | **INFO** | surface present after 2 s |
| F004Restyled | screenshot (portrait, restyled factory) | **INFO** | 02-f004-portrait.png |
| F004Restyled | landscape reached | **PASS** | ROTATION_90 2400x1080 |
| F004Restyled | screenshot (landscape, restyled factory) | **INFO** | 03-f004-landscape.png |
| F004Restyled | portrait restored | **PASS** | ROTATION_0 1080x2400 |
| F004Restyled | same process | **PASS** | PID 13569 |
| F004Crispness | portrait 1080x2400 band 0.25,0.6 uniform blocks | **PASS** | n=3 device px, phase (0,2), edge concentration x 0,991 y 0,980, 22540/22930 edge-bearing blocks uniform (98,3 %, floor 95,0 %). Screenshot: 04-f004-crisp-portrait.png |
| F004Crispness | landscape 2400x1080 band 0.25,0.6 uniform blocks | **FAIL** | n=2 device px, phase (1,0), edge concentration x 0,930 y 0,948, 27260/28949 edge-bearing blocks uniform (94,2 %, floor 95,0 %). Screenshot: 05-f004-crisp-landscape.png |
| Home | departure checkpoint written | **PASS** | gen 9, id 11d59074, run 1, cash 5683287, 12 machine(s), levels 331, prestige 0, data 7f4c95e3423d -> gen 10, id 62eaf6ca, run 1, cash 5683413, 12 machine(s), levels 331, prestige 0, data 5170809761bf |
| Home | return in the same process | **PASS** | PID 13569 -> 13569 |
| Home | return checkpoint written | **PASS** | gen 11, id 12f55277, run 1, cash 5683875, 12 machine(s), levels 331, prestige 0, data 76442c74ead4 |
| Home | screenshot (offline return summary) | **INFO** | 06-home-return.png |
| Lock | device asleep | **PASS** | mWakefulness=Asleep |
| Lock | departure checkpoint written | **PASS** | gen 12, id a87d838b, run 1, cash 5684025, 12 machine(s), levels 331, prestige 0, data ca4824c0eb00 |
| Lock | awake in the same process | **PASS** | awake=True, PID 13569 -> 13569 |
| Lock | return checkpoint written | **PASS** | gen 13, id 17409fe1, run 1, cash 5684451, 12 machine(s), levels 331, prestige 0, data 7c9754c98e5e |
| Lock | screenshot | **INFO** | 07-lock-return.png |
| ColdRestart | process stopped | **PASS** | PID 13569 gone |
| ColdRestart | new process | **PASS** | PID 13569 -> 13992 |
| ColdRestart | time to graphics device | **INFO** | 2 s |
| ColdRestart | first scene loaded (playable shell) | **PASS** | 4 s after the graphics device |
| ColdRestart | reconciled from the last durable checkpoint | **PASS** | gen 14, id 57ad61c1, run 1, cash 5684877, 12 machine(s), levels 331, prestige 0, data 0b89bfc0c604 |
| ColdRestart | machine set restored | **PASS** | 12 -> 12 machine(s) |
| ColdRestart | screenshot (restored factory before any input) | **INFO** | 08-cold-restart.png |
| ProducingFps | control-1 capture | **INFO** | 2466 frames, 111 dropped, 41.846 fps over 60 s; same process True |
| ProducingFps | producing pushed run loaded | **PASS** | reconciled in 290 ms from 22222222-2222-2222-2222-000000000030; gen 1, id 7d14daa5, run 1, cash 5681865, 12 machine(s), levels 331, prestige 0, data 3e904fb5d28d |
| ProducingFps | producing capture | **INFO** | 2491 frames, 102 dropped, 42.325 fps over 60 s; same process True |
| ProducingFps | control-2 capture | **INFO** | 2473 frames, 101 dropped, 42.006 fps over 60 s; same process True |
| ProducingFps | profiler-free build | **PASS** | 0 Autoconnect Profiler line(s) in the producing capture (the frame-rate build must not autoconnect the profiler) |
| ProducingFps | producing average FPS | **FAIL** | 42,325 fps (budget >= 60, owner decision 2026-09-29) |
| ProducingFps | same-session control (context) | **INFO** | fresh-run control mean 41.9 fps (controls: 41,846, 42,006); producing 42.325 fps |
| F004LogScan | no crash signatures | **PASS** | 0 FATAL/F DEBUG/SIGSEGV line(s) in 8787 |
| F004LogScan | no coded diagnostics | **PASS** | 0 persistence/start-up diagnostic line(s) |
| F004LogScan | E Unity lines | **INFO** | 6, of which 0 outside the known benign AssetPackManager and Autoconnect Profiler probes |

Result: FAIL (2 check(s)). Raw evidence: this directory.
