---
title: Player-level test gaps
type: concept
sources: [S001]
updated: 2026-09-28
---

# Player-level test gaps

The Feature 001 suites passed (Edit Mode 557, Play Mode up to 139) while the first Windows and Android player runs found several player-visible defects. Each defect sat behind a test seam that replaced exactly the component that failed. (S001)

| Defect | Seam that hid it | Closing test |
|---|---|---|
| No checkpoint could be written in the running game (T052) | Every persistence test used millisecond-aligned fake clocks or `CompositionTestOverrides` | `PlayerCheckpointRoundTripTests`: real `DeviceUtcClock`, real on-disk `JsonCheckpointStore`, no scene, runnable inside Windows and Android test players |
| HUD panels overlapped on a phone (T053) | Layout tests sized the document root explicitly | Assertions on the real root at the authored panel scale |
| Restored factory invisible after a cold start (T061) | Scene tests only seeded an in-memory checkpoint store | `ColdStartRestoreRenderTests`: writes a real checkpoint, hands a fresh store to the bootstrap and asserts rendered buildings before any input |
| World badges stale after production (T057-F1, fixed by T062) | Views were refreshed by the commands under test, not by the simulation | `NavigationPlacementTests` advance production across a state change without reselecting and assert badge, summary and no rebuild |

(S001)

## Lessons

- For each test double, add at least one test that uses the real component on the real platform path: clock, file store, scene bootstrap and panel settings. (S001)
- Diagnose before fixing. T038 guessed that managed code stripping broke Newtonsoft reflection. Privacy-safe `CheckpointDiagnostics` (exception type, stage and `member:line`, never messages, IDs or paths) located the real throw site and disproved the guess. No `link.xml` was needed. (S001)
- Composed evidence is not end-to-end evidence. The T041 reconciliation found that SC-002 (D001 completes a cycle, output is removed, the second cycle repeats) was proven only in pieces: the first cycle on the capacity-1 D001 fixture and the repeat on a capacity-2 fixture. T067 closed T041-F2 with one literal D001 test that completes the first cycle, asserts blocking while output is full, removes output, and completes the second cycle; Edit Mode passed 570/570. (S001)
- Record player findings as evidence and remediate them through convergence tasks. T038 and T057 changed no code. Their findings became T052–T062, each with its own regression test and independent reviews. All of them were closed by 2026-09-27. (S001)

Related: [local checkpoints and offline return](./local-checkpoints-and-offline-return.md), [UI Toolkit HUD layout pitfalls](./ui-toolkit-hud-layout-pitfalls.md), [Android emulator validation](./android-emulator-validation.md), [Feature acceptance evidence](./feature-acceptance-evidence.md).
