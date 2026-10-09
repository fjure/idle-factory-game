---
title: Player-level test gaps
type: concept
sources: [S001, S008, S018]
updated: 2026-10-09
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
- T119 retired the D001 scenario and its scene suites with the Feature 001 runtime, so the D001 evidence below is historical; the run's player journey is T121's (see [Feature 001 retirement](./feature-001-retirement.md)). (S018)
- Composed evidence is not end-to-end evidence. The T041 reconciliation found that SC-002 (D001 completes a cycle, output is removed, the second cycle repeats) was proven only in pieces: the first cycle on the capacity-1 D001 fixture and the repeat on a capacity-2 fixture. T067 closed T041-F2 with one literal D001 test that completes the first cycle, asserts blocking while output is full, removes output, and completes the second cycle; Edit Mode passed 570/570. (S001)
- Record player findings as evidence and remediate them through convergence tasks. T038 and T057 changed no code. Their findings became T052–T062, each with its own regression test and independent reviews. All of them were closed by 2026-09-27. (S001)

- Editor-only seams do not reach players. `CoreGameFoundationBootstrap`'s composition overrides are `#if UNITY_EDITOR`. A player test must therefore drive the real component directly, the way `PlayerCheckpointRoundTripTests` does, rather than extend that seam. Feature 002 plans its SC-004 content-failure evidence this way. (S008)
- An absence check must confirm through the authoritative store, not a cache. `AssetDatabase.GUIDToAssetPath` can return a deleted asset's path from a stale `Library/`, so the retired-GUID guard (T244) counts a path only when the file exists. (S018)
- T121 (#203) closes the run journey in the player: `RunJourneyPlayerTests` and `RunTransitionPlayerTests` use real `RunRig` composition with temp-root isolation. Interruption is simulated at the store boundary (`FailAfterPromoteStore`). Fixtures reach the player only as `Resources` copies (the one scene-free way to package a TextAsset), so they also ship in release builds; excluding them is a follow-up. A SHA-256 parity test with a negative case keeps copies equal to the originals. Run 2 is only 1.7 % faster than run 1 (2340 vs 2380 s): deterministic, but brittle under balance changes. (S018)
- Re-run the suites after the last edit. A worker changed code after its final Edit/Play run; QA reran on the exact commit. (S018)

- Test-local catalogs hide invariant violations. Every W003 fixture used `W003Workload` (12 slots at level 1), which the shipped content rejects (4 slots). T245 builds device sets from the shipped catalog (`device/w003-s-shipped`, `device/prestige-ready`) and `DeviceSetShippedCatalogTests` loads them through the player cold-start composition, with a negative test naming the slot-count invariant. (S018)
- Content revisions break UI tests that hard-code content (T125: 87 Play Mode panel tests). Derive expectations from the shipped catalog. (S018)
- Editor-green is not player-green. The T125 fix put `RunShellFixtures.ShippedCatalog` (via `AssetDatabase`) outside `#if UNITY_EDITOR` in `RunShellTestSupport.cs`; Edit Mode, editor Play Mode and QA passed, but `-StandaloneWindowsPlayer` failed to compile (exit 3, no result XML) until commit ac4d719 moved the guard. Prove changes to Play Mode test support with the Windows test-player run. (S018)
- T124 left two product gaps, both now closed: a HOME or focus cycle of a few seconds opened the modal Welcome-back summary (T246 suppresses it below a 60 s threshold, see [Run panels](./run-panels.md)), and the OS back gesture seemed not to cancel confirmations (T247: it cancels on the second swipe in immersive fullscreen, an accepted limitation, see [Android back gesture](./android-back-gesture.md)). A single "does nothing" observation needed a control (Back key, two swipes, Settings sub page) before it counted as a defect. (S018)

Related: [W003 player timing baseline](./w003-player-timing-baseline.md), [JSON content authoring](./json-content-authoring.md), [local checkpoints and offline return](./local-checkpoints-and-offline-return.md), [UI Toolkit HUD layout pitfalls](./ui-toolkit-hud-layout-pitfalls.md), [Android emulator validation](./android-emulator-validation.md), [Feature acceptance evidence](./feature-acceptance-evidence.md).
