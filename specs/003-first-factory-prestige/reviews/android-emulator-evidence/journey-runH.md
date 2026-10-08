# Android emulator journey - 2026-10-08 22:35

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
| UnityBuild | `Development, il2cpp, arm64-v8a` |

| Step | Check | Result | Detail |
|---|---|---|---|
| Environment | emulator lease | **INFO** | emulator: renewed for 'issue-206' (81 min) |
| Preflight | device booted | **PASS** | emulator-5554 |
| Preflight | expected AVD | **PASS** | Medium_Phone_API_36.0, API 36 |
| Preflight | arm64-v8a via translation | **PASS** | x86_64,arm64-v8a; bridge libndk_translation.so |
| Preflight | GLES renderer | **INFO** | Google (NVIDIA Corporation), Android Emulator OpenGL ES Translator (NVIDIA GeForce RTX 5070 Ti/PCIe/SSE2), OpenGL ES 3.1 (4.5.0 NVIDIA 591.86) |
| Install | adb install -r | **PASS** | Success |
| Install | pm clear | **PASS** | Success |
| Launch | process started | **PASS** | PID 5770 |
| Launch | time to graphics device | **INFO** | 2 s after process start |
| Launch | primary ABI | **PASS** | arm64-v8a |
| Launch | Unity build | **INFO** | Development, il2cpp, arm64-v8a |
| Launch | graphics device | **PASS** | Creating OpenGL ES 3.1 graphics device ; Context level  <OpenGL ES 3.1> ; Context handle -1016684464 |
| Launch | time to first display | **INFO** | 1s410ms |
| Launch | display | **INFO** | ROTATION_0 1080x2400, requested SCREEN_ORIENTATION_FULL_USER |
| Launch | screenshot | **INFO** | 01-launch.png |
| TouchBuild | touchscreen feature | **PASS** | android.hardware.touchscreen present |
| TouchBuild | display | **INFO** | ROTATION_0 1080x2400, requested SCREEN_ORIENTATION_FULL_USER |
| TouchBuild | screenshot before | **INFO** | 02-touch-before.png |
| TouchBuild | baseline departure checkpoint | **FAIL** | none |
| TouchBuild | UI ready before the first tap | **INFO** | surface present after 2 s |
| TouchBuild | screenshot after | **INFO** | 03-touch-after.png |
| TouchBuild | machine built by touch | **FAIL** | none -> gen 3, id 1a6bfbc9, run 1, cash 15, 1 machine(s), prestige 0, data c37ad4f355fc |
| TouchBuild | same process | **PASS** | PID 5770 |
| RemoveConfirmRotation | baseline departure checkpoint | **PASS** | gen 5, id 04a97efe, run 1, cash 15, 1 machine(s), prestige 0, data 150e5c8683d6 |
| RemoveConfirmRotation | UI ready before the first tap | **INFO** | surface present after 2 s |
| RemoveConfirmRotation | screenshot confirmation open | **INFO** | 04-RemoveConfirmRotation-open.png |
| RemoveConfirmRotation | rotated to landscape | **PASS** | ROTATION_90 2400x1080, requested SCREEN_ORIENTATION_FULL_USER |
| RemoveConfirmRotation | screenshot after rotation (dialog cancelled, offline summary withheld) | **INFO** | 05-RemoveConfirmRotation-rotated.png |
| RemoveConfirmRotation | same process | **PASS** | PID 5770 |
| RemoveConfirmRotation | return reconcile (R20: allowed) | **INFO** | 1 ResumeAndReconcile line(s), ms: 32 |
| RemoveConfirmRotation | no coded diagnostics during the rotation | **PASS** | no return.blocked / save.v2.* / persistence diagnostic while the dialog was open and rotated |
| RemoveConfirmRotation | offline summary withheld | **INFO** | the player logs no marker for the summary panel; judge it on the screenshots RemoveConfirmRotation-open and RemoveConfirmRotation-rotated (no Welcome back dialog, notice Cancelled by rotation) |
| RemoveConfirmRotation | nothing spent | **PASS** | gen 5, id 04a97efe, run 1, cash 15, 1 machine(s), prestige 0, data 150e5c8683d6 -> gen 9, id 6c884e5a, run 1, cash 15, 1 machine(s), prestige 0, data 6f999d99d792 |
| BackGesture | gesture navigation active | **PASS** | navigation mode before the step: gestural |
| BackGesture | baseline departure checkpoint | **PASS** | gen 13, id 06304a88, run 1, cash 15, 1 machine(s), prestige 0, data a23d7c3cfeec |
| BackGesture | UI ready before the first tap | **INFO** | surface present after 2 s |
| BackGesture | screenshot confirmation open | **INFO** | 06-BackGesture-open.png |
| BackGesture | screenshot after the back gesture (dialog closed, focus on the Remove button) | **INFO** | 07-BackGesture-closed.png |
| BackGesture | same process | **PASS** | PID 5770 |
| BackGesture | activity stays resumed (the gesture did not leave the app) | **PASS** | resumed activity of com.DefaultCompany.idlefactorygame |
| BackGesture | nothing spent (machine kept) | **PASS** | gen 13, id 06304a88, run 1, cash 15, 1 machine(s), prestige 0, data a23d7c3cfeec -> gen 15, id 2b9c3830, run 1, cash 15, 1 machine(s), prestige 0, data 4cf4c82d9aea |

Result: FAIL (2 check(s)). Raw evidence: this directory.
