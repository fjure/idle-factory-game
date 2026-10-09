# Android emulator journey - 2026-10-09 12:12

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
| Apk | `C:\Users\Domenik\ai-projects\idle-factory-game\.maqa-worktrees\issue-328\test-results\t247\baseline-pbon.apk (36970449 bytes, SHA-256 b4c871fdb422b5761818ce050c18cb97aa8ab946e6abdbe2aa5f924542bedd6b)` |
| UnityBuild | `Development, il2cpp, arm64-v8a` |

| Step | Check | Result | Detail |
|---|---|---|---|
| Environment | emulator lease | **INFO** | emulator: renewed for 'issue-328' (81 min) |
| Preflight | device booted | **PASS** | emulator-5554 |
| Preflight | expected AVD | **PASS** | Medium_Phone_API_36.0, API 36 |
| Preflight | arm64-v8a via translation | **PASS** | x86_64,arm64-v8a; bridge libndk_translation.so |
| Preflight | GLES renderer | **INFO** | Google (NVIDIA Corporation), Android Emulator OpenGL ES Translator (NVIDIA GeForce RTX 5070 Ti/PCIe/SSE2), OpenGL ES 3.1 (4.5.0 NVIDIA 591.86) |
| Install | adb install -r | **PASS** | Success |
| Install | pm clear | **PASS** | Success |
| Launch | process started | **PASS** | PID 23272 |
| Launch | time to graphics device | **INFO** | 4 s after process start |
| Launch | primary ABI | **PASS** | arm64-v8a |
| Launch | Unity build | **INFO** | Development, il2cpp, arm64-v8a |
| Launch | graphics device | **PASS** | Creating OpenGL ES 3.1 graphics device ; Context level  <OpenGL ES 3.1> ; Context handle -1016661136 |
| Launch | time to first display | **INFO** | 3s705ms |
| Launch | display | **INFO** | ROTATION_0 1080x2400, requested SCREEN_ORIENTATION_FULL_USER |
| Launch | screenshot | **INFO** | 01-launch.png |
| TouchBuild | touchscreen feature | **PASS** | android.hardware.touchscreen present |
| TouchBuild | display | **INFO** | ROTATION_0 1080x2400, requested SCREEN_ORIENTATION_FULL_USER |
| TouchBuild | screenshot before | **INFO** | 02-touch-before.png |
| TouchBuild | baseline departure checkpoint | **PASS** | gen 1, id b206dd9a, run 1, cash 37, 0 machine(s), prestige 0, data f830f732b5b6 |
| TouchBuild | UI ready before the first tap | **INFO** | surface present after 2 s |
| TouchBuild | screenshot after | **INFO** | 03-touch-after.png |
| TouchBuild | machine built by touch | **PASS** | gen 1, id b206dd9a, run 1, cash 37, 0 machine(s), prestige 0, data f830f732b5b6 -> gen 3, id 8bf90d19, run 1, cash 7, 1 machine(s), prestige 0, data 9d346a9baa36 |
| TouchBuild | same process | **PASS** | PID 23272 |
| RemoveConfirmRotation | baseline departure checkpoint | **PASS** | gen 5, id 0ad7a80f, run 1, cash 7, 1 machine(s), prestige 0, data 09601e2a51f8 |
| RemoveConfirmRotation | UI ready before the first tap | **INFO** | surface present after 2 s |
| RemoveConfirmRotation | screenshot confirmation open | **INFO** | 04-RemoveConfirmRotation-open.png |
| RemoveConfirmRotation | rotated to landscape | **PASS** | ROTATION_90 2400x1080, requested SCREEN_ORIENTATION_FULL_USER |
| RemoveConfirmRotation | screenshot after rotation (dialog cancelled, offline summary withheld) | **INFO** | 05-RemoveConfirmRotation-rotated.png |
| RemoveConfirmRotation | same process | **PASS** | PID 23272 |
| RemoveConfirmRotation | return reconcile (R20: allowed) | **INFO** | 1 ResumeAndReconcile line(s), ms: 54 |
| RemoveConfirmRotation | no coded diagnostics during the rotation | **PASS** | no return.blocked / save.v2.* / persistence diagnostic while the dialog was open and rotated |
| RemoveConfirmRotation | offline summary withheld | **INFO** | the player logs no marker for the summary panel; judge it on the screenshots RemoveConfirmRotation-open and RemoveConfirmRotation-rotated (no Welcome back dialog, notice Cancelled by rotation) |
| RemoveConfirmRotation | nothing spent | **PASS** | gen 5, id 0ad7a80f, run 1, cash 7, 1 machine(s), prestige 0, data 09601e2a51f8 -> gen 9, id aa7d3bb8, run 1, cash 7, 1 machine(s), prestige 0, data 571be45a05db |
| BackGesture | gesture navigation active | **PASS** | navigation mode before the step: gestural; input under test: Gesture x 1 swipe(s) |
| BackGesture | baseline departure checkpoint | **PASS** | gen 13, id 3c532f3c, run 1, cash 7, 1 machine(s), prestige 0, data fe4b49980ef1 |
| BackGesture | UI ready before the first tap | **INFO** | surface present after 2 s |
| BackGesture | screenshot confirmation open (attempt 1) | **INFO** | 07-BackGesture-open.png, 29637 danger-button pixels (>= 5000 means the dialog is on screen) |
| BackGesture | SystemUI state before the input (INFO: navbar_hidden with backGestureDisabled=true means immersive mode, the first edge swipe only reveals the system bars) | **INFO** | navbar_hidden/awake; backGestureDisabled=true |
| BackGesture | screenshot after the back input (DECISIVE MANUAL CHECK: dialog closed, focus ring on the Remove button; a single swipe in immersive mode only reveals the system bars) | **INFO** | 08-BackGesture-closed.png |
| BackGesture | Gesture cancels the confirmation (machine-checked: danger-button pixels) | **FAIL** | danger-button pixels before the input 29637 (needs >= 5000), after 29637 (needs < 500) |
| BackGesture | guard: same process (also true when the input did nothing) | **PASS** | PID 23272 |
| BackGesture | guard: activity stays resumed (the input did not leave the app) | **PASS** | resumed activity of com.DefaultCompany.idlefactorygame |
| BackGesture | guard: nothing spent, machine kept (also true when the input did nothing) | **PASS** | gen 13, id 3c532f3c, run 1, cash 7, 1 machine(s), prestige 0, data fe4b49980ef1 -> gen 15, id f07241d9, run 1, cash 7, 1 machine(s), prestige 0, data e23c43ede6ab |
| BackGesture | back with no surface open (recorded, not asserted) | **INFO** | process alive: True (PID 23272), top activity: topResumedActivity=ActivityRecord{97348045 u0 com.DefaultCompany.idlefactorygame/com.idlefactory.player.IdleFactoryGameActivity t182}, screenshots 09-BackGesture-idle-before.png -> 10-BackGesture-idle-after.png, whole-screen change 0,001 |

Result: FAIL (1 check(s)). Raw evidence: this directory.
