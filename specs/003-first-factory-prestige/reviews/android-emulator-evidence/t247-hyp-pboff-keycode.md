# Android emulator journey - 2026-10-09 11:55

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
| Environment | emulator lease | **INFO** | emulator: renewed for 'issue-328' (81 min) |
| Preflight | device booted | **PASS** | emulator-5554 |
| Preflight | expected AVD | **PASS** | Medium_Phone_API_36.0, API 36 |
| Preflight | arm64-v8a via translation | **PASS** | x86_64,arm64-v8a; bridge libndk_translation.so |
| Preflight | GLES renderer | **INFO** | Google (NVIDIA Corporation), Android Emulator OpenGL ES Translator (NVIDIA GeForce RTX 5070 Ti/PCIe/SSE2), OpenGL ES 3.1 (4.5.0 NVIDIA 591.86) |
| BackGesture | gesture navigation active | **PASS** | navigation mode before the step: gestural; input under test: Keycode |
| BackGesture | baseline departure checkpoint | **PASS** | gen 25, id f984c3c1, run 1, cash 7, 1 machine(s), prestige 0, data 59dbc3702105 |
| BackGesture | UI ready before the first tap | **INFO** | surface present after 2 s |
| BackGesture | screenshot confirmation open (attempt 1) | **INFO** | 02-BackGesture-open.png, 29637 danger-button pixels (>= 5000 means the dialog is on screen) |
| BackGesture | screenshot after the back input (DECISIVE MANUAL CHECK: dialog closed, focus ring on the Remove button; a single swipe in immersive mode only reveals the system bars) | **INFO** | 03-BackGesture-closed.png |
| BackGesture | Keycode cancels the confirmation (machine-checked: danger-button pixels) | **PASS** | danger-button pixels before the input 29637 (needs >= 5000), after 0 (needs < 500) |
| BackGesture | guard: same process (also true when the input did nothing) | **PASS** | PID 18472 |
| BackGesture | guard: activity stays resumed (the input did not leave the app) | **PASS** | resumed activity of com.DefaultCompany.idlefactorygame |
| BackGesture | guard: nothing spent, machine kept (also true when the input did nothing) | **PASS** | gen 25, id f984c3c1, run 1, cash 7, 1 machine(s), prestige 0, data 59dbc3702105 -> gen 27, id 83f8bb48, run 1, cash 7, 1 machine(s), prestige 0, data 701ae8161049 |
| BackGesture | back with no surface open (recorded, not asserted) | **INFO** | process alive: True (PID 18472), top activity: topResumedActivity=ActivityRecord{263351746 u0 com.DefaultCompany.idlefactorygame/com.idlefactory.player.IdleFactoryGameActivity t178}, screenshots 04-BackGesture-idle-before.png -> 05-BackGesture-idle-after.png, whole-screen change 0,492 |

Result: PASS. Raw evidence: this directory.
