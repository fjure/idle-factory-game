# Android emulator journey - 2026-10-09 12:15

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
| PrestigeConfirmRotation | taps | **INFO** | default: Continue, Prestige tab, scroll, scroll, Sell the factory (1080x2400 fractions) |
| PrestigeConfirmRotation | committed prestige-ready/run-checkpoint.data-a.json | **PASS** | SHA-256 4849e8f8d051f114 |
| PrestigeConfirmRotation | committed prestige-ready/run-checkpoint.manifest-a.bin | **PASS** | SHA-256 46d98136301dbc39 |
| PrestigeConfirmRotation | device prestige-ready/run-checkpoint.data-a.json | **PASS** | SHA-256 4849e8f8d051f114 on the device |
| PrestigeConfirmRotation | device prestige-ready/run-checkpoint.manifest-a.bin | **PASS** | SHA-256 46d98136301dbc39 on the device |
| PrestigeConfirmRotation | prestige-ready run loaded | **PASS** | accepted by the player after 10 s |
| PrestigeConfirmRotation | baseline departure checkpoint | **PASS** | gen 2, id 38019e30, run 1, cash 20467535, 10 machine(s), prestige 0, data 3b930af17f44 |
| PrestigeConfirmRotation | UI ready before the first tap | **INFO** | surface present after 2 s |
| PrestigeConfirmRotation | screenshot confirmation open | **INFO** | 01-PrestigeConfirmRotation-open.png |
| PrestigeConfirmRotation | rotated to landscape | **PASS** | ROTATION_90 2400x1080, requested SCREEN_ORIENTATION_FULL_USER |
| PrestigeConfirmRotation | screenshot after rotation (dialog cancelled, offline summary withheld) | **INFO** | 02-PrestigeConfirmRotation-rotated.png |
| PrestigeConfirmRotation | same process | **PASS** | PID 24721 |
| PrestigeConfirmRotation | return reconcile (R20: allowed) | **INFO** | 1 ResumeAndReconcile line(s), ms: 73 |
| PrestigeConfirmRotation | no coded diagnostics during the rotation | **PASS** | no return.blocked / save.v2.* / persistence diagnostic while the dialog was open and rotated |
| PrestigeConfirmRotation | offline summary withheld | **INFO** | the player logs no marker for the summary panel; judge it on the screenshots PrestigeConfirmRotation-open and PrestigeConfirmRotation-rotated (no Welcome back dialog, notice Cancelled by rotation) |
| PrestigeConfirmRotation | nothing spent | **PASS** | gen 2, id 38019e30, run 1, cash 20467535, 10 machine(s), prestige 0, data 3b930af17f44 -> gen 6, id 616d6e55, run 1, cash 20477399, 10 machine(s), prestige 0, data aa347a879951 |

Result: PASS. Raw evidence: this directory.
