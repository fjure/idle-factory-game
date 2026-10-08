# Android emulator journey - 2026-10-08 21:34

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
| Launch | process started | **PASS** | PID 4970 |
| Launch | time to graphics device | **INFO** | 2 s after process start |
| Launch | primary ABI | **PASS** | arm64-v8a |
| Launch | Unity build | **INFO** | Development, il2cpp, arm64-v8a |
| Launch | graphics device | **PASS** | Creating OpenGL ES 3.1 graphics device ; Context level  <OpenGL ES 3.1> ; Context handle 774393520 |
| Launch | time to first display | **INFO** | 1s610ms |
| Launch | display | **INFO** | ROTATION_0 1080x2400, requested SCREEN_ORIENTATION_FULL_USER |
| Launch | screenshot | **INFO** | 01-launch.png |
| TouchBuild | touchscreen feature | **PASS** | android.hardware.touchscreen present |
| TouchBuild | display | **INFO** | ROTATION_0 1080x2400, requested SCREEN_ORIENTATION_FULL_USER |
| TouchBuild | screenshot before | **INFO** | 02-touch-before.png |
| TouchBuild | screenshot after | **INFO** | 03-touch-after.png |
| TouchBuild | machine built by touch | **FAIL** | none -> none |
| TouchBuild | same process | **PASS** | PID 4970 |
| RemoveConfirmRotation | screenshot confirmation open | **INFO** | 04-RemoveConfirmRotation-open.png |
| RemoveConfirmRotation | rotated to landscape | **PASS** | ROTATION_90 2400x1080, requested SCREEN_ORIENTATION_FULL_USER |
| RemoveConfirmRotation | screenshot after rotation (dialog cancelled, no offline summary) | **INFO** | 05-RemoveConfirmRotation-rotated.png |
| RemoveConfirmRotation | same process | **PASS** | PID 4970 |
| RemoveConfirmRotation | no reconcile (no offline summary) | **FAIL** | no ResumeAndReconcile line while the dialog was open and rotated |
| RemoveConfirmRotation | nothing spent | **FAIL** | no Save V2 checkpoint on the device to compare (before: none, after: gen 2, id 881f0fd0, run 1, cash 25, 0 machine(s), prestige 0, data da5c699215a4) |

Result: FAIL (3 check(s)). Raw evidence: this directory.
