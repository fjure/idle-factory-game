# Android emulator journey - 2026-10-09 22:48

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
| Apk | `C:\Users\Domenik\ai-projects\idle-factory-game\.maqa-worktrees\issue-350\test-results\t251\fixed.apk (51048581 bytes, SHA-256 e62e2402c92864e599e4ab779f7396cb22107722102119f4abb17655e9789455)` |
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
| Launch | process started | **PASS** | PID 11742 |
| Launch | time to graphics device | **INFO** | 4 s after process start |
| Launch | primary ABI | **PASS** | arm64-v8a |
| Launch | Unity build | **INFO** | Development, il2cpp, arm64-v8a |
| Launch | graphics device | **PASS** | Creating OpenGL ES 3.1 graphics device ; Context level  <OpenGL ES 3.1> ; Context handle -615837744 |
| Launch | time to first display | **INFO** | 2s253ms |
| Launch | display | **INFO** | ROTATION_0 1080x2400, requested SCREEN_ORIENTATION_FULL_USER |
| Launch | screenshot | **INFO** | 01-launch.png |
| TouchBuild | touchscreen feature | **PASS** | android.hardware.touchscreen present |
| TouchBuild | display | **INFO** | ROTATION_0 1080x2400, requested SCREEN_ORIENTATION_FULL_USER |
| TouchBuild | screenshot before | **INFO** | 02-touch-before.png |
| TouchBuild | baseline departure checkpoint | **PASS** | gen 1, id 3418b1ce, run 1, cash 37, 0 machine(s), prestige 0, data a4ab67b50424 |
| TouchBuild | UI ready before the first tap | **INFO** | surface present after 2 s |
| TouchBuild | screenshot after | **INFO** | 03-touch-after.png |
| TouchBuild | machine built by touch | **PASS** | gen 1, id 3418b1ce, run 1, cash 37, 0 machine(s), prestige 0, data a4ab67b50424 -> gen 3, id 0d615505, run 1, cash 7, 1 machine(s), prestige 0, data fdeef1ebb536 |
| TouchBuild | same process | **PASS** | PID 11742 |
| RemoveConfirmRotation | baseline departure checkpoint | **PASS** | gen 5, id 172e3186, run 1, cash 7, 1 machine(s), prestige 0, data 18d95cd86bd0 |
| RemoveConfirmRotation | UI ready before the first tap | **INFO** | surface present after 2 s |
| RemoveConfirmRotation | screenshot confirmation open | **INFO** | 04-RemoveConfirmRotation-open.png |
| RemoveConfirmRotation | rotated to landscape | **PASS** | ROTATION_90 2400x1080, requested SCREEN_ORIENTATION_FULL_USER |
| RemoveConfirmRotation | screenshot after rotation (dialog cancelled, offline summary withheld) | **INFO** | 05-RemoveConfirmRotation-rotated.png |
| RemoveConfirmRotation | same process | **PASS** | PID 11742 |
| RemoveConfirmRotation | return reconcile (R20: allowed) | **INFO** | 1 ResumeAndReconcile line(s), ms: 29 |
| RemoveConfirmRotation | no coded diagnostics during the rotation | **PASS** | no return.blocked / save.v2.* / persistence diagnostic while the dialog was open and rotated |
| RemoveConfirmRotation | offline summary withheld | **INFO** | the player logs no marker for the summary panel; judge it on the screenshots RemoveConfirmRotation-open and RemoveConfirmRotation-rotated (no Welcome back dialog, notice Cancelled by rotation) |
| RemoveConfirmRotation | nothing spent | **PASS** | gen 5, id 172e3186, run 1, cash 7, 1 machine(s), prestige 0, data 18d95cd86bd0 -> gen 9, id 3c01bfb2, run 1, cash 7, 1 machine(s), prestige 0, data f351d738873b |
| BackGesture | gesture navigation active | **PASS** | navigation mode before the step: gestural; input under test: Keycode |
| BackGesture | baseline departure checkpoint | **PASS** | gen 13, id 7460cd42, run 1, cash 7, 1 machine(s), prestige 0, data 8fff7d37d4b6 |
| BackGesture | UI ready before the first tap | **INFO** | surface present after 2 s |
| BackGesture | screenshot confirmation open (attempt 1) | **INFO** | 07-BackGesture-open.png, 29637 danger-button pixels (>= 5000 means the dialog is on screen) |
| BackGesture | SystemUI state before the input (INFO: navbar_hidden with backGestureDisabled=true means immersive mode, the first edge swipe only reveals the system bars) | **INFO** | navbar_hidden/awake; backGestureDisabled=true |
| BackGesture | screenshot after the back input (DECISIVE MANUAL CHECK: dialog closed, focus ring on the Remove button; a single swipe in immersive mode only reveals the system bars) | **INFO** | 08-BackGesture-closed.png |
| BackGesture | Keycode cancels the confirmation (machine-checked: danger-button pixels) | **PASS** | danger-button pixels before the input 29637 (needs >= 5000), after 0 (needs < 500) |
| BackGesture | guard: same process (also true when the input did nothing) | **PASS** | PID 11742 |
| BackGesture | guard: activity stays resumed (the input did not leave the app) | **PASS** | resumed activity of com.DefaultCompany.idlefactorygame |
| BackGesture | guard: nothing spent, machine kept (also true when the input did nothing) | **PASS** | gen 13, id 7460cd42, run 1, cash 7, 1 machine(s), prestige 0, data 8fff7d37d4b6 -> gen 15, id 4370578b, run 1, cash 7, 1 machine(s), prestige 0, data 34039c3718ed |
| BackGesture | back with no surface open (recorded, not asserted) | **INFO** | process alive: True (PID 11742), top activity: topResumedActivity=ActivityRecord{193158523 u0 com.DefaultCompany.idlefactorygame/com.idlefactory.player.IdleFactoryGameActivity t206}, screenshots 09-BackGesture-idle-before.png -> 10-BackGesture-idle-after.png, whole-screen change 0,489 |
| PrestigeConfirmRotation | taps | **INFO** | default: Continue, Prestige tab, scroll, scroll, Sell the factory (1080x2400 fractions) |
| PrestigeConfirmRotation | committed prestige-ready/run-checkpoint.data-a.json | **PASS** | SHA-256 4849e8f8d051f114 |
| PrestigeConfirmRotation | committed prestige-ready/run-checkpoint.manifest-a.bin | **PASS** | SHA-256 46d98136301dbc39 |
| PrestigeConfirmRotation | device prestige-ready/run-checkpoint.data-a.json | **PASS** | SHA-256 4849e8f8d051f114 on the device |
| PrestigeConfirmRotation | device prestige-ready/run-checkpoint.manifest-a.bin | **PASS** | SHA-256 46d98136301dbc39 on the device |
| PrestigeConfirmRotation | prestige-ready run loaded | **PASS** | accepted by the player after 13 s |
| PrestigeConfirmRotation | baseline departure checkpoint | **PASS** | gen 2, id 4c522247, run 1, cash 20468327, 10 machine(s), prestige 0, data 555dba92b00d |
| PrestigeConfirmRotation | UI ready before the first tap | **INFO** | surface present after 2 s |
| PrestigeConfirmRotation | screenshot confirmation open | **INFO** | 11-PrestigeConfirmRotation-open.png |
| PrestigeConfirmRotation | rotated to landscape | **PASS** | ROTATION_90 2400x1080, requested SCREEN_ORIENTATION_FULL_USER |
| PrestigeConfirmRotation | screenshot after rotation (dialog cancelled, offline summary withheld) | **INFO** | 12-PrestigeConfirmRotation-rotated.png |
| PrestigeConfirmRotation | same process | **PASS** | PID 12539 |
| PrestigeConfirmRotation | return reconcile (R20: allowed) | **INFO** | 1 ResumeAndReconcile line(s), ms: 88 |
| PrestigeConfirmRotation | no coded diagnostics during the rotation | **PASS** | no return.blocked / save.v2.* / persistence diagnostic while the dialog was open and rotated |
| PrestigeConfirmRotation | offline summary withheld | **INFO** | the player logs no marker for the summary panel; judge it on the screenshots PrestigeConfirmRotation-open and PrestigeConfirmRotation-rotated (no Welcome back dialog, notice Cancelled by rotation) |
| PrestigeConfirmRotation | nothing spent | **PASS** | gen 2, id 4c522247, run 1, cash 20468327, 10 machine(s), prestige 0, data 555dba92b00d -> gen 6, id 76a50587, run 1, cash 20477807, 10 machine(s), prestige 0, data f2752b56a7cc |

Result: PASS. Raw evidence: this directory.
