# Android emulator journey - 2026-10-09 22:42

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
| Apk | `C:\Users\Domenik\ai-projects\idle-factory-game\.maqa-worktrees\issue-350\test-results\t251\A-noRIB.apk (36972321 bytes, SHA-256 e33038a8c19a7c9033e295d60efc0775cb0bd5e556b5dc5c456f0f4551034bf4)` |
| UnityBuild | `Development, il2cpp, arm64-v8a` |

| Step | Check | Result | Detail |
|---|---|---|---|
| Environment | emulator lease | **INFO** | emulator: renewed for 'issue-350' (81 min) |
| Preflight | device booted | **PASS** | emulator-5554 |
| Preflight | expected AVD | **PASS** | Medium_Phone_API_36.0, API 36 |
| Preflight | arm64-v8a via translation | **PASS** | x86_64,arm64-v8a; bridge libndk_translation.so |
| Preflight | GLES renderer | **INFO** | Google (NVIDIA Corporation), Android Emulator OpenGL ES Translator (NVIDIA GeForce RTX 5070 Ti/PCIe/SSE2), OpenGL ES 3.1 (4.5.0 NVIDIA 591.86) |
| Install | adb install -r | **PASS** | Success |
| Install | pm clear | **PASS** | Success |
| Launch | process started | **PASS** | PID 10047 |
| Launch | time to graphics device | **INFO** | 2 s after process start |
| Launch | primary ABI | **PASS** | arm64-v8a |
| Launch | Unity build | **INFO** | Development, il2cpp, arm64-v8a |
| Launch | graphics device | **PASS** | Creating OpenGL ES 3.1 graphics device ; Context level  <OpenGL ES 3.1> ; Context handle -615830544 |
| Launch | time to first display | **INFO** | 1s797ms |
| Launch | display | **INFO** | ROTATION_0 1080x2400, requested SCREEN_ORIENTATION_FULL_USER |
| Launch | screenshot | **INFO** | 01-launch.png |
| TouchBuild | touchscreen feature | **PASS** | android.hardware.touchscreen present |
| TouchBuild | display | **INFO** | ROTATION_0 1080x2400, requested SCREEN_ORIENTATION_FULL_USER |
| TouchBuild | screenshot before | **INFO** | 02-touch-before.png |
| TouchBuild | baseline departure checkpoint | **PASS** | gen 1, id 03ec8dfe, run 1, cash 37, 0 machine(s), prestige 0, data edf32b82f012 |
| TouchBuild | UI ready before the first tap | **INFO** | surface present after 2 s |
| TouchBuild | screenshot after | **INFO** | 03-touch-after.png |
| TouchBuild | machine built by touch | **PASS** | gen 1, id 03ec8dfe, run 1, cash 37, 0 machine(s), prestige 0, data edf32b82f012 -> gen 3, id 53a14bea, run 1, cash 7, 1 machine(s), prestige 0, data 1f9513e36a95 |
| TouchBuild | same process | **PASS** | PID 10047 |
| BackGesture | gesture navigation active | **PASS** | navigation mode before the step: gestural; input under test: Keycode |
| BackGesture | baseline departure checkpoint | **PASS** | gen 5, id 2121d933, run 1, cash 7, 1 machine(s), prestige 0, data 954edeff113e |
| BackGesture | UI ready before the first tap | **INFO** | surface present after 2 s |
| BackGesture | screenshot confirmation open (attempt 1) | **INFO** | 05-BackGesture-open.png, 29637 danger-button pixels (>= 5000 means the dialog is on screen) |
| BackGesture | SystemUI state before the input (INFO: navbar_hidden with backGestureDisabled=true means immersive mode, the first edge swipe only reveals the system bars) | **INFO** | navbar_hidden/awake; backGestureDisabled=true |
| BackGesture | screenshot after the back input (DECISIVE MANUAL CHECK: dialog closed, focus ring on the Remove button; a single swipe in immersive mode only reveals the system bars) | **INFO** | 06-BackGesture-closed.png |
| BackGesture | Keycode cancels the confirmation (machine-checked: danger-button pixels) | **PASS** | danger-button pixels before the input 29637 (needs >= 5000), after 0 (needs < 500) |
| BackGesture | guard: same process (also true when the input did nothing) | **PASS** | PID 10047 |
| BackGesture | guard: activity stays resumed (the input did not leave the app) | **PASS** | resumed activity of com.DefaultCompany.idlefactorygame |
| BackGesture | guard: nothing spent, machine kept (also true when the input did nothing) | **PASS** | gen 5, id 2121d933, run 1, cash 7, 1 machine(s), prestige 0, data 954edeff113e -> gen 7, id 54388217, run 1, cash 7, 1 machine(s), prestige 0, data 7ff51c9d6020 |
| BackGesture | back with no surface open (recorded, not asserted) | **INFO** | process alive: True (PID 10047), top activity: topResumedActivity=ActivityRecord{159924791 u0 com.DefaultCompany.idlefactorygame/com.idlefactory.player.IdleFactoryGameActivity t203}, screenshots 07-BackGesture-idle-before.png -> 08-BackGesture-idle-after.png, whole-screen change 0,447 |

Result: PASS. Raw evidence: this directory.
