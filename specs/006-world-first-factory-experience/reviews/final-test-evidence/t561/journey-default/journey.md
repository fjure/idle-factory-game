# Android emulator journey - 2026-11-25 12:33

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
| Environment | emulator lease | **INFO** | emulator: renewed for 'issue-782' (37 min) |
| Preflight | device booted | **PASS** | emulator-5554 |
| Preflight | expected AVD | **PASS** | IdleFactory_Test_API_36, API 36 |
| Preflight | arm64-v8a via translation | **PASS** | x86_64,arm64-v8a; bridge libndk_translation.so |
| Preflight | GLES renderer | **INFO** | Google (NVIDIA Corporation), Android Emulator OpenGL ES Translator (NVIDIA GeForce RTX 5070 Ti/PCIe/SSE2), OpenGL ES 3.1 (4.5.0 NVIDIA 621.01) |
| Install | /data free space | **PASS** | 3486 MB free (56 percent used), floor 400 MB |
| Install | adb install -r | **PASS** | Success |
| Install | pm clear | **PASS** | Success |
| Launch | process started | **PASS** | PID 16418 |
| Launch | time to graphics device | **INFO** | 2 s after process start |
| Launch | first scene loaded (playable shell) | **PASS** | 4 s after the graphics device |
| Launch | primary ABI | **PASS** | arm64-v8a |
| Launch | Unity build | **INFO** | Development, il2cpp, arm64-v8a |
| Launch | graphics device | **PASS** | Creating OpenGL ES 3.1 graphics device ; Context level  <OpenGL ES 3.1> ; Context handle -2091495536 |
| Launch | time to first display | **INFO** | 1s522ms |
| Launch | display | **INFO** | ROTATION_0 1080x2400, requested SCREEN_ORIENTATION_FULL_USER |
| Launch | screenshot | **INFO** | 01-launch.png |
| Rotation | sensor +x | **PASS** | ROTATION_90 2400x1080, requested SCREEN_ORIENTATION_FULL_USER (expected ROTATION_90, FULL_USER) |
| Rotation | screenshot | **INFO** | 02-landscape.png |
| Rotation | user-rotation lock 0 with sensor +x | **PASS** | ROTATION_0 1080x2400, requested SCREEN_ORIENTATION_FULL_USER (expected ROTATION_0, FULL_USER) |
| Rotation | user-rotation free | **PASS** | ROTATION_90 2400x1080, requested SCREEN_ORIENTATION_FULL_USER (expected ROTATION_90, FULL_USER) |
| Rotation | sensor -x | **PASS** | ROTATION_270 2400x1080, requested SCREEN_ORIENTATION_FULL_USER (expected ROTATION_270, FULL_USER) |
| Rotation | sensor +y | **PASS** | ROTATION_0 1080x2400, requested SCREEN_ORIENTATION_FULL_USER (expected ROTATION_0, FULL_USER) |
| Rotation | same process throughout | **PASS** | PID 16418 -> 16418 |
| Home | departure checkpoint written | **PASS** | gen 11, id ed0e3c80, run 1, cash 37, 0 machine(s), levels 0, prestige 0, data 3764cc6e91a2 -> gen 12, id 1513b6d4, run 1, cash 37, 0 machine(s), levels 0, prestige 0, data f51c3dbc5362 |
| Home | return in the same process | **PASS** | PID 16418 -> 16418 |
| Home | return checkpoint written | **PASS** | gen 13, id e16183cf, run 1, cash 37, 0 machine(s), levels 0, prestige 0, data 13577dc759e1 |
| Home | screenshot (offline return summary) | **INFO** | 03-home-return.png |
| Lock | device asleep | **PASS** | mWakefulness=Asleep |
| Lock | departure checkpoint written | **PASS** | gen 14, id 2d6f28b5, run 1, cash 37, 0 machine(s), levels 0, prestige 0, data f7991b8523ef |
| Lock | awake in the same process | **PASS** | awake=True, PID 16418 -> 16418 |
| Lock | return checkpoint written | **PASS** | gen 15, id 0ea48e0f, run 1, cash 37, 0 machine(s), levels 0, prestige 0, data 262eb43d5e4e |
| Lock | screenshot | **INFO** | 04-lock-return.png |
| ColdRestart | process stopped | **PASS** | PID 16418 gone |
| ColdRestart | new process | **PASS** | PID 16418 -> 16758 |
| ColdRestart | time to graphics device | **INFO** | 2 s |
| ColdRestart | first scene loaded (playable shell) | **PASS** | 4 s after the graphics device |
| ColdRestart | reconciled from the last durable checkpoint | **PASS** | gen 16, id eb133342, run 1, cash 37, 0 machine(s), levels 0, prestige 0, data d0be76e336ee |
| ColdRestart | machine set restored | **PASS** | 0 -> 0 machine(s) |
| ColdRestart | screenshot (restored factory before any input) | **INFO** | 05-cold-restart.png |
| LogScan | no crash signatures | **PASS** | 0 FATAL/F DEBUG/SIGSEGV line(s) in 4638 |
| LogScan | no persistence diagnostics | **PASS** | 0 [IdleFactory.Persistence] line(s) |
| LogScan | E Unity lines | **INFO** | 2 (known benign: AssetPackManager and Autoconnect Profiler probes, two per process) |

Result: PASS. Raw evidence: this directory.
