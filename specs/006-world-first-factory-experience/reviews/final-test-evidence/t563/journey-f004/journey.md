# Android emulator journey - 2026-11-25 20:46

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
| Apk | `C:\Users\Domenik\ai-projects\idle-factory-game\.maqa-worktrees\issue-786-t563-upgrade-marker\test-results\android\t563\CoreGameFoundation-noautoconnect-gles3.apk (46723082 bytes, SHA-256 9491c37126d939a86c02e5963d6197ceb46621dd9de8921cbd80a2130b940e6f)` |
| UnityBuild | `Development, il2cpp, arm64-v8a` |
| W004VHash | `1920dfafb75e69131fe43bb38f66c088762b195c46aa7916c568747c7ecca6fc` |

| Step | Check | Result | Detail |
|---|---|---|---|
| Environment | emulator lease | **INFO** | emulator: renewed for 'issue-786' (77 min) |
| Preflight | device booted | **PASS** | emulator-5554 |
| Preflight | expected AVD | **PASS** | IdleFactory_Test_API_36, API 36 |
| Preflight | arm64-v8a via translation | **PASS** | x86_64,arm64-v8a; bridge libndk_translation.so |
| Preflight | GLES renderer | **INFO** | Google (NVIDIA Corporation), Android Emulator OpenGL ES Translator (NVIDIA GeForce RTX 5070 Ti/PCIe/SSE2), OpenGL ES 3.1 (4.5.0 NVIDIA 621.01) |
| Install | /data free space | **PASS** | 3477 MB free (56 percent used), floor 400 MB |
| Install | adb install -r | **PASS** | Success |
| Install | pm clear | **PASS** | Success |
| Launch | process started | **PASS** | PID 5954 |
| Launch | time to graphics device | **INFO** | 4 s after process start |
| Launch | first scene loaded (playable shell) | **PASS** | 4 s after the graphics device |
| Launch | primary ABI | **PASS** | arm64-v8a |
| Launch | Unity build | **INFO** | Development, il2cpp, arm64-v8a |
| Launch | graphics device | **PASS** | Creating OpenGL ES 3.1 graphics device ; Context level  <OpenGL ES 3.1> ; Context handle 199882576 |
| Launch | time to first display | **INFO** | 2s972ms |
| Launch | display | **INFO** | ROTATION_0 1080x2400, requested SCREEN_ORIENTATION_FULL_USER |
| Launch | screenshot | **INFO** | 01-launch.png |
| W004VPush | W004-V fixture present | **PASS** | Assets/IdleFactory/Tests/PlayMode/Fixtures/W004V/W004VFixture.cs |
| W004VPush | W004-V fixture hash recorded | **PASS** | ExpectedHash 1920dfafb75e69131fe43bb38f66c088762b195c46aa7916c568747c7ecca6fc |
| W004VPush | committed w003-s-shipped/run-checkpoint.data-a.json | **PASS** | SHA-256 4f7ed559fb1a396a |
| W004VPush | committed w003-s-shipped/run-checkpoint.manifest-a.bin | **PASS** | SHA-256 781810f0d39f3069 |
| W004VPush | device w003-s-shipped/run-checkpoint.data-a.json | **PASS** | SHA-256 4f7ed559fb1a396a on the device |
| W004VPush | device w003-s-shipped/run-checkpoint.manifest-a.bin | **PASS** | SHA-256 781810f0d39f3069 on the device |
| F004Restyled | W004-V base set loaded | **PASS** | reconciled from 22222222-2222-2222-2222-000000000030 after 11 s; gen 1, id eefc0e8b, run 1, cash 5681865, 12 machine(s), levels 331, prestige 0, data 633decee6eda |
| F004Restyled | UI ready before the first tap | **INFO** | surface present after 2 s |
| F004Restyled | screenshot (portrait, restyled factory) | **INFO** | 02-f004-portrait.png |
| F004Restyled | landscape reached | **PASS** | ROTATION_90 2400x1080 |
| F004Restyled | screenshot (landscape, restyled factory) | **INFO** | 03-f004-landscape.png |
| F004Restyled | portrait restored | **PASS** | ROTATION_0 1080x2400 |
| F004Restyled | same process | **PASS** | PID 6209 |
| F004Crispness | portrait 1080x2400 band 0.25,0.6 x 0,1 uniform blocks | **PASS** | n=3 device px, phase (0,2), edge concentration x 0,991 y 0,987, 22600/22861 edge-bearing blocks uniform (98,9 %, floor 95,0 %). Screenshot: 04-f004-crisp-portrait.png |
| F004Crispness | landscape 2400x1080 band 0.25,0.6 x 0.1,0.9 uniform blocks | **PASS** | n=4 device px, phase (1,0), edge concentration x 0,999 y 0,969, 11167/11439 edge-bearing blocks uniform (97,6 %, floor 95,0 %). Screenshot: 05-f004-crisp-landscape.png |
| Home | departure checkpoint written | **PASS** | gen 9, id 0e7a7258, run 1, cash 5683341, 12 machine(s), levels 331, prestige 0, data ca43baf92a0d -> gen 10, id 8404d838, run 1, cash 5683485, 12 machine(s), levels 331, prestige 0, data 028e763d0b24 |
| Home | return in the same process | **PASS** | PID 6209 -> 6209 |
| Home | return checkpoint written | **PASS** | gen 11, id 70a4aa68, run 1, cash 5683977, 12 machine(s), levels 331, prestige 0, data 18019c25e7f2 |
| Home | screenshot (offline return summary) | **INFO** | 06-home-return.png |
| Lock | device asleep | **PASS** | mWakefulness=Asleep |
| Lock | departure checkpoint written | **PASS** | gen 12, id 56d1bd59, run 1, cash 5684235, 12 machine(s), levels 331, prestige 0, data d0b89a6a4d86 |
| Lock | awake in the same process | **PASS** | awake=True, PID 6209 -> 6209 |
| Lock | return checkpoint written | **PASS** | gen 13, id d54310d2, run 1, cash 5684661, 12 machine(s), levels 331, prestige 0, data 87ed3dd5ac1e |
| Lock | screenshot | **INFO** | 07-lock-return.png |
| Environment | adb server stable | **FAIL** | adb server restarted during: adb pull /sdcard/Android/data/com.DefaultCompany.idlefactorygame/files/IdleFactory/Checkpoints/v2/run-checkpoint.manifest-a.bin C:\Users\Domenik\ai-projects\idle-factory-game\.maqa-worktrees\issue-786-t563-upgrade-marker\test-results\android\t563\journey-f004\checkpoints\cold-0-before\run-checkpoint.manifest-a.bin. Another process killed it (for example a Unity run with Android as active target); discard this run. |
| ColdRestart | process stopped | **PASS** | PID 6209 gone |
| ColdRestart | new process | **PASS** | PID 6209 -> 6715 |
| ColdRestart | time to graphics device | **INFO** | 6 s |
| ColdRestart | first scene loaded (playable shell) | **PASS** | 6 s after the graphics device |
| ColdRestart | reconciled from the last durable checkpoint | **PASS** | gen 15, id d0cae4b2, run 1, cash 5686563, 12 machine(s), levels 331, prestige 0, data 2a6f90f76de2 |
| ColdRestart | machine set restored | **PASS** | 12 -> 12 machine(s) |
| ColdRestart | screenshot (restored factory before any input) | **INFO** | 08-cold-restart.png |
| ProducingFps | control-1 capture | **INFO** | 3174 frames, 135 dropped, 54.255 fps over 60 s; same process True |
| ProducingFps | producing pushed run loaded | **PASS** | reconciled in 424 ms from 22222222-2222-2222-2222-000000000030; gen 1, id 16a65d0c, run 1, cash 5681865, 12 machine(s), levels 331, prestige 0, data 7c2ac49f2efb |
| ProducingFps | producing capture | **INFO** | 1963 frames, 1 dropped, 33.016 fps over 60 s; same process True |
| ProducingFps | control-2 capture | **INFO** | 3349 frames, 102 dropped, 57.345 fps over 60 s; same process True |
| ProducingFps | profiler-free build | **PASS** | 0 Autoconnect Profiler line(s) in the producing capture (the frame-rate build must not autoconnect the profiler) |
| ProducingFps | producing average FPS | **FAIL** | 33,016 fps (budget >= 60, owner decision 2026-09-29) |
| ProducingFps | same-session control (context) | **INFO** | fresh-run control mean 55.8 fps (controls: 54,255, 57,345); producing 33.016 fps |
| F004LogScan | no crash signatures | **PASS** | 0 FATAL/F DEBUG/SIGSEGV line(s) in 10994 |
| F004LogScan | no coded diagnostics | **PASS** | 0 persistence/start-up diagnostic line(s) |
| F004LogScan | E Unity lines | **INFO** | 6, of which 0 outside the known benign AssetPackManager and Autoconnect Profiler probes |

Result: FAIL (2 check(s)). Raw evidence: this directory.
