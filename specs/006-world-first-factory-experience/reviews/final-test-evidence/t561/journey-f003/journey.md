# Android emulator journey - 2026-11-25 12:34

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
| Environment | emulator lease | **INFO** | emulator: renewed for 'issue-782' (81 min) |
| Preflight | device booted | **PASS** | emulator-5554 |
| Preflight | expected AVD | **PASS** | IdleFactory_Test_API_36, API 36 |
| Preflight | arm64-v8a via translation | **PASS** | x86_64,arm64-v8a; bridge libndk_translation.so |
| Preflight | GLES renderer | **INFO** | Google (NVIDIA Corporation), Android Emulator OpenGL ES Translator (NVIDIA GeForce RTX 5070 Ti/PCIe/SSE2), OpenGL ES 3.1 (4.5.0 NVIDIA 621.01) |
| Install | /data free space | **PASS** | 3486 MB free (56 percent used), floor 400 MB |
| Install | adb install -r | **PASS** | Success |
| Install | pm clear | **PASS** | Success |
| Launch | process started | **PASS** | PID 16986 |
| Launch | time to graphics device | **INFO** | 2 s after process start |
| Launch | first scene loaded (playable shell) | **PASS** | 4 s after the graphics device |
| Launch | primary ABI | **PASS** | arm64-v8a |
| Launch | Unity build | **INFO** | Development, il2cpp, arm64-v8a |
| Launch | graphics device | **PASS** | Creating OpenGL ES 3.1 graphics device ; Context level  <OpenGL ES 3.1> ; Context handle -2091524336 |
| Launch | time to first display | **INFO** | 1s641ms |
| Launch | display | **INFO** | ROTATION_0 1080x2400, requested SCREEN_ORIENTATION_FULL_USER |
| Launch | screenshot | **INFO** | 01-launch.png |
| TouchBuild | touchscreen feature | **PASS** | android.hardware.touchscreen present |
| TouchBuild | display | **INFO** | ROTATION_0 1080x2400, requested SCREEN_ORIENTATION_FULL_USER |
| TouchBuild | screenshot before | **INFO** | 02-touch-before.png |
| TouchBuild | build by touch | **INFO** | skipped: no -BuildTaps supplied (layout dependent; T124 supplies them from the screenshots) |
| RemoveConfirmRotation | confirmation cancelled by rotation | **INFO** | skipped: no taps supplied for this confirmation (layout dependent; T124 supplies them) |
| BackGesture | back gesture cancels the confirmation | **INFO** | skipped: no -RemoveTaps supplied (layout dependent; T124 supplies them) |
| PrestigeConfirmRotation | taps | **INFO** | default: Continue, hub entry, Prestige card, scroll, scroll, Sell the factory (fractions of the display size adb reports) |
| PrestigeConfirmRotation | committed prestige-ready/run-checkpoint.data-a.json | **PASS** | SHA-256 4849e8f8d051f114 |
| PrestigeConfirmRotation | committed prestige-ready/run-checkpoint.manifest-a.bin | **PASS** | SHA-256 46d98136301dbc39 |
| PrestigeConfirmRotation | device prestige-ready/run-checkpoint.data-a.json | **PASS** | SHA-256 4849e8f8d051f114 on the device |
| PrestigeConfirmRotation | device prestige-ready/run-checkpoint.manifest-a.bin | **PASS** | SHA-256 46d98136301dbc39 on the device |
| PrestigeConfirmRotation | prestige-ready run loaded | **PASS** | accepted by the player after 8 s |
| PrestigeConfirmRotation | baseline departure checkpoint | **PASS** | gen 2, id 0acd2242, run 1, cash 14708999, 10 machine(s), levels 400, prestige 0, data c2a14bac6c3f |
| PrestigeConfirmRotation | UI ready before the first tap | **INFO** | surface present after 2 s |
| PrestigeConfirmRotation | screenshot confirmation open | **INFO** | 03-PrestigeConfirmRotation-open.png |
| PrestigeConfirmRotation | rotated to landscape | **PASS** | ROTATION_90 2400x1080, requested SCREEN_ORIENTATION_FULL_USER |
| PrestigeConfirmRotation | screenshot after rotation (dialog cancelled, offline summary withheld) | **INFO** | 04-PrestigeConfirmRotation-rotated.png |
| PrestigeConfirmRotation | same process | **PASS** | PID 17149 |
| PrestigeConfirmRotation | return reconcile (R20: allowed) | **INFO** | 1 ResumeAndReconcile line(s), ms: 31 |
| PrestigeConfirmRotation | no coded diagnostics during the rotation | **PASS** | no return.blocked / save.v2.* / persistence diagnostic while the dialog was open and rotated |
| PrestigeConfirmRotation | offline summary withheld | **INFO** | the player logs no marker for the summary panel; judge it on the screenshots PrestigeConfirmRotation-open and PrestigeConfirmRotation-rotated (no Welcome back dialog, notice Cancelled by rotation) |
| PrestigeConfirmRotation | nothing spent | **PASS** | gen 2, id 0acd2242, run 1, cash 14708999, 10 machine(s), levels 400, prestige 0, data c2a14bac6c3f -> gen 6, id 66c8a350, run 1, cash 14713463, 10 machine(s), levels 400, prestige 0, data f925e33036d8 |
| W003Push | committed w003-s-shipped/run-checkpoint.data-a.json | **PASS** | SHA-256 4f7ed559fb1a396a |
| W003Push | committed w003-s-shipped/run-checkpoint.manifest-a.bin | **PASS** | SHA-256 781810f0d39f3069 |
| W003Push | device w003-s-shipped/run-checkpoint.data-a.json | **PASS** | SHA-256 4f7ed559fb1a396a on the device |
| W003Push | device w003-s-shipped/run-checkpoint.manifest-a.bin | **PASS** | SHA-256 781810f0d39f3069 on the device |
| OfflineReturnTiming | departure beyond the eight-hour cap | **PASS** | w003-s-shipped departure is 26.557 h before the device clock, so each accepted start reconciles exactly the capped 8 h |
| OfflineReturnTiming | start 1 | **PASS** | 302 ms; accepted, reconciled from w003-s-shipped; gen 1, id 2a7eaa29, run 1, cash 5681865, 12 machine(s), levels 331, prestige 0, data 31962b9bbb8b |
| OfflineReturnTiming | start 2 | **PASS** | 312 ms; accepted, reconciled from w003-s-shipped; gen 1, id 85d55a83, run 1, cash 5681865, 12 machine(s), levels 331, prestige 0, data c7390fa2d72b |
| OfflineReturnTiming | start 3 | **PASS** | 295 ms; accepted, reconciled from w003-s-shipped; gen 1, id e8ace9d7, run 1, cash 5681865, 12 machine(s), levels 331, prestige 0, data 46d9c86fbe7a |
| OfflineReturnTiming | median of accepted starts | **PASS** | median 302 ms of [302, 312, 295] accepted (3 of 3 starts accepted; budget <= 2000 ms, PE-2) |
| OfflineReturnTiming | w003-s-shipped keeps producing | **PASS** | gen 1, id e8ace9d7, run 1, cash 5681865, 12 machine(s), levels 331, prestige 0, data 46d9c86fbe7a -> gen 2, id 951b58f3, run 1, cash 5682921, 12 machine(s), levels 331, prestige 0, data eca4bc05f10e |
| ProducingFps | control-1 capture | **INFO** | 2371 frames, 44 dropped, 40.140 fps over 60 s; same process True |
| ProducingFps | producing pushed run loaded | **PASS** | reconciled in 287 ms from 22222222-2222-2222-2222-000000000030; gen 1, id 34b10c64, run 1, cash 5681865, 12 machine(s), levels 331, prestige 0, data e2d92df1643e |
| ProducingFps | producing capture | **INFO** | 2370 frames, 57 dropped, 40.144 fps over 60 s; same process True |
| ProducingFps | control-2 capture | **INFO** | 2355 frames, 65 dropped, 39.869 fps over 60 s; same process True |
| ProducingFps | profiler-free build | **PASS** | 0 Autoconnect Profiler line(s) in the producing capture (the frame-rate build must not autoconnect the profiler) |
| ProducingFps | producing average FPS | **FAIL** | 40,144 fps (budget >= 60, owner decision 2026-09-29) |
| ProducingFps | same-session control (context) | **INFO** | fresh-run control mean 40 fps (controls: 40,14, 39,869); producing 40.144 fps |
| F003LogScan | no crash signatures | **PASS** | 0 FATAL/F DEBUG/SIGSEGV line(s) in 12545 |
| F003LogScan | no coded diagnostics | **PASS** | 0 persistence/start-up diagnostic line(s) |
| F003LogScan | E Unity lines | **INFO** | 9, of which 0 outside the known benign AssetPackManager and Autoconnect Profiler probes |

Result: FAIL (1 check(s)). Raw evidence: this directory.
